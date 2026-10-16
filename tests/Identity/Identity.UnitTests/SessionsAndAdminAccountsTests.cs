using ParkingSystem.Identity.Application.Accounts.AdminAccounts;
using ParkingSystem.Identity.Application.Accounts.Sessions;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class SessionsAndAdminAccountsTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36", "Chrome trên Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36 Edg/141.0", "Edge trên Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari trên iOS")]
    [InlineData("Mozilla/5.0 (Linux; Android 15) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Mobile Safari/537.36", "Chrome trên Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:131.0) Gecko/20100101 Firefox/131.0", "Firefox trên macOS")]
    [InlineData("PostmanRuntime/7.42.0", "Postman")]
    [InlineData("curl/8.9.1", "curl")]
    [InlineData(null, "Thiết bị không xác định")]
    public void Device_is_described_from_the_user_agent(string? userAgent, string expected) =>
        Assert.Equal(expected, DeviceDescription.FromUserAgent(userAgent));

    [Theory]
    [InlineData("113.161.25.77", "113.161.x.x")]
    [InlineData("::ffff:192.168.1.20", "192.168.x.x")]
    [InlineData("2001:db8:85a3:12:8a2e:370:7334:1", "2001:db8:85a3:12:x:x:x:x")]
    [InlineData("not-an-ip", null)]
    [InlineData(null, null)]
    public void Ip_address_keeps_only_the_network_part(string? ip, string? expected) =>
        Assert.Equal(expected, DeviceDescription.MaskIp(ip));

    [Theory]
    [InlineData(null, null, null, 1, 20, true)]
    [InlineData("nguyen", "staff", "EmailVerified", 2, 100, true)]
    [InlineData(null, "Root", null, 1, 20, false)]
    [InlineData(null, null, "Deleted", 1, 20, false)]
    [InlineData(null, null, null, 0, 20, false)]
    [InlineData(null, null, null, 1, 101, false)]
    public void Search_validator_accepts_only_known_filters_and_bounded_paging(
        string? search, string? role, string? status, int page, int pageSize, bool expectedValid)
    {
        var result = new SearchAccountsQueryValidator().Validate(new SearchAccountsQuery(search, role, status, page, pageSize));
        Assert.Equal(expectedValid, result.IsValid);
    }
}
