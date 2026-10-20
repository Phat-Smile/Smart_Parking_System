using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

// A delayed or redelivered OTP event must not mail a code that has already expired.
public sealed class OtpEmailConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(300, true)]
    [InlineData(0, false)]
    [InlineData(-60, false)]
    public async Task Verification_email_is_sent_only_while_the_code_is_valid(int secondsLeft, bool expectedSent)
    {
        var sender = new RecordingEmailSender();
        await using var provider = BuildHarness<UserVerificationOtpIssuedConsumer>(sender);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new UserVerificationOtpIssuedIntegrationEvent(
            Guid.NewGuid(), "user@example.test", "012345", Now.AddSeconds(secondsLeft)), TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<UserVerificationOtpIssuedConsumer>()
            .Consumed.Any<UserVerificationOtpIssuedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Equal(expectedSent ? 1 : 0, sender.VerificationEmails);
    }

    [Theory]
    [InlineData(300, true)]
    [InlineData(0, false)]
    public async Task Password_reset_email_is_sent_only_while_the_code_is_valid(int secondsLeft, bool expectedSent)
    {
        var sender = new RecordingEmailSender();
        await using var provider = BuildHarness<PasswordResetOtpIssuedConsumer>(sender);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new PasswordResetOtpIssuedIntegrationEvent(
            Guid.NewGuid(), "user@example.test", "012345", Now.AddSeconds(secondsLeft)), TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<PasswordResetOtpIssuedConsumer>()
            .Consumed.Any<PasswordResetOtpIssuedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Equal(expectedSent ? 1 : 0, sender.PasswordResetEmails);
    }

    private static ServiceProvider BuildHarness<TConsumer>(RecordingEmailSender sender)
        where TConsumer : class, IConsumer =>
        new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IEmailSender>(sender)
            .AddSingleton<IClock>(new FixedClock(Now))
            .AddMassTransitTestHarness(configurator => configurator.AddConsumer<TConsumer>())
            .BuildServiceProvider(true);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int VerificationEmails { get; private set; }
        public int PasswordResetEmails { get; private set; }

        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            VerificationEmails++;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            PasswordResetEmails++;
            return Task.CompletedTask;
        }
    }
}
