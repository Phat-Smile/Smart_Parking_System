using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Notification.Infrastructure.Email;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class SmtpEmailSenderTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SmtpEmailSender_rethrows_exception_on_send_failure_without_swallowing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SMTP_HOST"] = "invalid.smtp.domain.local",
                ["SMTP_PORT"] = "587",
                ["SMTP_USERNAME"] = "test-user",
                ["SMTP_PASSWORD"] = "test-pass"
            })
            .Build();

        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sender.SendVerificationOtpEmailAsync(
                "user@example.com",
                "123456",
                InitialTime.AddMinutes(5),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("an.nguyen@example.com", "a***@example.com")]
    [InlineData("  B@x.vn ", "B***@x.vn")]
    [InlineData("no-at-sign", "***")]
    [InlineData("@example.com", "***")]
    public void Recipient_email_is_masked_in_logs(string email, string expected)
    {
        Assert.Equal(expected, SmtpEmailSender.MaskEmail(email));
    }
}
