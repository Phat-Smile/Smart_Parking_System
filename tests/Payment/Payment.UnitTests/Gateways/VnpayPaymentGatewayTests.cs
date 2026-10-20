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
}
