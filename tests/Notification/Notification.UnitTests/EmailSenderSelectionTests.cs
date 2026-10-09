using Microsoft.Extensions.Configuration;
using ParkingSystem.Notification.Infrastructure;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

// The console sender logs the OTP code; Program refuses to start with it outside Development.
public sealed class EmailSenderSelectionTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData("console", null, true)]
    [InlineData("CONSOLE", null, true)]
    [InlineData("smtp.example.com", null, false)]
    [InlineData(null, "smtp.example.com", false)]
    public void Console_sender_is_selected_only_without_a_real_smtp_host(string? smtpHostVariable, string? smtpHostSetting, bool expectedConsole)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SMTP_HOST"] = smtpHostVariable,
                ["Smtp:Host"] = smtpHostSetting
            })
            .Build();

        Assert.Equal(expectedConsole, DependencyInjection.UsesConsoleEmailSender(configuration));
    }
}
