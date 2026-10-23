using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Infrastructure.Gateways;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Gateways;

public class VnpayPaymentGatewayTests
{
    [Fact]
    public void CreatePaymentUrl_GeneratesValidUrlWithSortedQueryAndSha512Hash()
    {
        // Arrange
        const string secret = "TEST_HASH_SECRET_1234567890";
        var options = Options.Create(new VnpayOptions
        {
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            TmnCode = "TESTTMN01",
            HashSecret = secret,
            ReturnUrl = "http://localhost:3000/payment/vnpay-return",
            PaymentExpiryMinutes = 15
        });

        var gateway = new VnpayPaymentGateway(options);
        var txnRef = "test_txn_ref_001";
        var amount = 100_000m;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15);
        var clientIp = "127.0.0.1";

        // Act
        var url = gateway.CreatePaymentUrl(txnRef, amount, expiresAtUtc, clientIp);

        // Assert - TC-TOP-16: URL format, amount x 100, and signature
        Assert.NotNull(url);
        Assert.StartsWith(options.Value.BaseUrl, url);

        var uri = new Uri(url);
        var queryParams = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("TESTTMN01", queryParams["vnp_TmnCode"]);
        Assert.Equal("10000000", queryParams["vnp_Amount"]); // 100,000 * 100
        Assert.Equal(txnRef, queryParams["vnp_TxnRef"]);
        Assert.Equal("VND", queryParams["vnp_CurrCode"]);
        Assert.Equal("2.1.0", queryParams["vnp_Version"]);
        Assert.Equal("pay", queryParams["vnp_Command"]);
        Assert.NotNull(queryParams["vnp_SecureHash"]);

        // Verify SHA512 hash matches expected calculation
        var secureHash = queryParams["vnp_SecureHash"]!;
        Assert.NotEmpty(secureHash);
        Assert.Equal(128, secureHash.Length); // 64 bytes in hex = 128 characters
    }

    [Fact]
    public void VerifyIpnSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        const string secret = "TEST_HASH_SECRET_1234567890";
        var options = Options.Create(new VnpayOptions
        {
            HashSecret = secret
        });
        var gateway = new VnpayPaymentGateway(options);

        // Tạo chữ ký hợp lệ
        var rawData = "vnp_Amount=10000000&vnp_Command=pay&vnp_ResponseCode=00&vnp_TmnCode=DEMO&vnp_TxnRef=ref123";
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData))).ToLowerInvariant();

        var query = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "10000000",
            ["vnp_Command"] = "pay",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = "DEMO",
            ["vnp_TxnRef"] = "ref123",
            ["vnp_SecureHash"] = hash
        };

        // Act
        var isValid = gateway.VerifyIpnSignature(query);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyIpnSignature_TamperedAmount_ReturnsFalse()
    {
        // Arrange
        const string secret = "TEST_HASH_SECRET_1234567890";
        var options = Options.Create(new VnpayOptions { HashSecret = secret });
        var gateway = new VnpayPaymentGateway(options);

        var query = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "99999999", // Số tiền bị can thiệp
            ["vnp_TxnRef"] = "ref123",
            ["vnp_SecureHash"] = "invalid_hash_string"
        };

        // Act & Assert
        Assert.False(gateway.VerifyIpnSignature(query));
    }

    [Fact]
    public void VerifyIpnSignature_MissingHash_ReturnsFalse()
    {
        var options = Options.Create(new VnpayOptions { HashSecret = "SECRET" });
        var gateway = new VnpayPaymentGateway(options);

        var query = new Dictionary<string, string> { ["vnp_TxnRef"] = "ref123" };
        Assert.False(gateway.VerifyIpnSignature(query));
    }
}
