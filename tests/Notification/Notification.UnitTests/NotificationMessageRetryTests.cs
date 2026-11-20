using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Infrastructure.Consumers;
using ParkingSystem.Notification.Infrastructure.Messaging;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class NotificationMessageRetryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Production_retry_constants_match_approved_specification()
    {
        Assert.Equal(5, NotificationRetryPolicy.DefaultRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(2), NotificationRetryPolicy.DefaultMinInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), NotificationRetryPolicy.DefaultMaxInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), NotificationRetryPolicy.DefaultIntervalDelta);
    }

    [Fact]
    public async Task T_09_Transient_TimeoutException_retries_and_succeeds_without_publishing_fault()
    {
        var sender = new MockEmailSender(failuresBeforeSuccess: 2);
        await using var provider = BuildRetryHarness(sender, new FixedClock(Now));
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var ct = TestContext.Current.CancellationToken;
            var message = new UserVerificationOtpIssuedIntegrationEvent(
                Guid.NewGuid(), "user@example.test", "123456", Now.AddMinutes(5));

            await harness.Bus.Publish(message, ct);

            var consumerHarness = harness.GetConsumerHarness<UserVerificationOtpIssuedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<UserVerificationOtpIssuedIntegrationEvent>(ct));

            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (sender.Attempts < 3 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(10, ct);
            }

            Assert.Equal(3, sender.Attempts);
            Assert.False(await harness.Published.Any<Fault<UserVerificationOtpIssuedIntegrationEvent>>(ct));
        }
        finally
        {
            await harness.Stop(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task T_10_Persistent_failure_retries_full_quota_and_publishes_fault()
    {
        var sender = new MockEmailSender(failuresBeforeSuccess: int.MaxValue);
        await using var provider = BuildRetryHarness(sender, new FixedClock(Now));
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var ct = TestContext.Current.CancellationToken;
            var message = new UserVerificationOtpIssuedIntegrationEvent(
                Guid.NewGuid(), "user@example.test", "123456", Now.AddMinutes(5));

            await harness.Bus.Publish(message, ct);

            Assert.True(await harness.Published.Any<Fault<UserVerificationOtpIssuedIntegrationEvent>>(ct));
            Assert.Equal(1 + NotificationRetryPolicy.DefaultRetryCount, sender.Attempts);
        }
        finally
        {
            await harness.Stop(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task T_11_Expired_OTP_is_skipped_and_never_dispatches_email()
    {
        var sender = new MockEmailSender(failuresBeforeSuccess: 0);
        await using var provider = BuildRetryHarness(sender, new FixedClock(Now));
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var ct = TestContext.Current.CancellationToken;
            var message = new UserVerificationOtpIssuedIntegrationEvent(
                Guid.NewGuid(), "user@example.test", "123456", Now.AddMinutes(-1));

            await harness.Bus.Publish(message, ct);

            var consumerHarness = harness.GetConsumerHarness<UserVerificationOtpIssuedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<UserVerificationOtpIssuedIntegrationEvent>(ct));
            Assert.Equal(0, sender.Attempts);
            Assert.False(await harness.Published.Any<Fault<UserVerificationOtpIssuedIntegrationEvent>>(ct));
        }
        finally
        {
            await harness.Stop(TestContext.Current.CancellationToken);
        }
    }

    private static ServiceProvider BuildRetryHarness(IEmailSender emailSender, IClock clock) =>
        new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(emailSender)
            .AddSingleton(clock)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<UserVerificationOtpIssuedConsumer>();
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageRetry(r => NotificationRetryPolicy.Configure(
                        r,
                        retryCount: NotificationRetryPolicy.DefaultRetryCount,
                        minInterval: TimeSpan.FromMilliseconds(5),
                        maxInterval: TimeSpan.FromMilliseconds(20),
                        intervalDelta: TimeSpan.FromMilliseconds(5)));
                    cfg.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class MockEmailSender(int failuresBeforeSuccess) : IEmailSender
    {
        public int Attempts { get; private set; }

        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= failuresBeforeSuccess)
            {
                throw new TimeoutException($"Simulated transient network timeout (attempt {Attempts}).");
            }

            return Task.CompletedTask;
        }

        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
