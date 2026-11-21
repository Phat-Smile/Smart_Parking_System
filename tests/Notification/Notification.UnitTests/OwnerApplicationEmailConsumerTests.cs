using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class OwnerApplicationEmailConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private const string TargetEmail = "owner.applicant@example.com";

    [Fact]
    public async Task Approved_consumer_dispatches_congratulatory_email()
    {
        var sender = new RecordingEmailSender();
        await using var provider = BuildHarness<OwnerApplicationApprovedConsumer>(sender);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var integrationEvent = new OwnerApplicationApprovedIntegrationEvent(
            AccountId,
            TargetEmail,
            Now);

        await harness.Bus.Publish(integrationEvent, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<OwnerApplicationApprovedConsumer>()
            .Consumed.Any<OwnerApplicationApprovedIntegrationEvent>(TestContext.Current.CancellationToken));

        Assert.Equal(1, sender.OwnerApprovedEmails);
        Assert.Equal(TargetEmail, sender.LastApprovedEmail);
        Assert.Equal(Now, sender.LastApprovedAtUtc);
    }

    [Fact]
    public async Task Rejected_consumer_dispatches_explanation_email()
    {
        var sender = new RecordingEmailSender();
        await using var provider = BuildHarness<OwnerApplicationRejectedConsumer>(sender);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        const string rejectionReason = "Giấy chứng nhận đăng ký kinh doanh không khớp thông tin.";
        var integrationEvent = new OwnerApplicationRejectedIntegrationEvent(
            AccountId,
            TargetEmail,
            rejectionReason,
            Now);

        await harness.Bus.Publish(integrationEvent, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<OwnerApplicationRejectedConsumer>()
            .Consumed.Any<OwnerApplicationRejectedIntegrationEvent>(TestContext.Current.CancellationToken));

        Assert.Equal(1, sender.OwnerRejectedEmails);
        Assert.Equal(TargetEmail, sender.LastRejectedEmail);
        Assert.Equal(rejectionReason, sender.LastRejectedReason);
        Assert.Equal(Now, sender.LastRejectedAtUtc);
    }

    private static ServiceProvider BuildHarness<TConsumer>(RecordingEmailSender sender)
        where TConsumer : class, IConsumer =>
        new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IEmailSender>(sender)
            .AddMassTransitTestHarness(configurator => configurator.AddConsumer<TConsumer>())
            .BuildServiceProvider(true);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int OwnerApprovedEmails { get; private set; }
        public string? LastApprovedEmail { get; private set; }
        public DateTimeOffset? LastApprovedAtUtc { get; private set; }

        public int OwnerRejectedEmails { get; private set; }
        public string? LastRejectedEmail { get; private set; }
        public string? LastRejectedReason { get; private set; }
        public DateTimeOffset? LastRejectedAtUtc { get; private set; }

        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default)
        {
            OwnerApprovedEmails++;
            LastApprovedEmail = email;
            LastApprovedAtUtc = approvedAtUtc;
            return Task.CompletedTask;
        }

        public Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default)
        {
            OwnerRejectedEmails++;
            LastRejectedEmail = email;
            LastRejectedReason = reason;
            LastRejectedAtUtc = rejectedAtUtc;
            return Task.CompletedTask;
        }

        public Task SendReservationArrivalReminderEmailAsync(
            string email,
            string reminderType,
            DateTime scheduledArrivalTimeUtc,
            DateTime cutoffTimeUtc,
            string? maskedPlateNumber,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
