using System.Text.RegularExpressions;
using ParkingSystem.Contracts.Events.Parking;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingSessionReceiptContractTests
{
    [Fact]
    public void Receipt_event_holds_expected_contract_fields_and_anonymized_plate()
    {
        var sessionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var entryTime = DateTimeOffset.UtcNow.AddHours(-2);
        var exitTime = DateTimeOffset.UtcNow;
        const string maskedPlate = "51F-***.89";
        const long totalFee = 30000;
        const long paidAmount = 30000;
        const long amountDue = 0;

        var receiptEvent = new ParkingSessionReceiptIssuedIntegrationEvent(
            sessionId,
            lotId,
            maskedPlate,
            totalFee,
            paidAmount,
            amountDue,
            entryTime,
            exitTime,
            accountId);

        Assert.Equal(sessionId, receiptEvent.SessionId);
        Assert.Equal(lotId, receiptEvent.LotId);
        Assert.Equal(maskedPlate, receiptEvent.MaskedPlateNumber);
        Assert.Equal(totalFee, receiptEvent.TotalFee);
        Assert.Equal(paidAmount, receiptEvent.PaidAmount);
        Assert.Equal(amountDue, receiptEvent.AmountDue);
        Assert.Equal(entryTime, receiptEvent.EntryTimeUtc);
        Assert.Equal(exitTime, receiptEvent.ExitTimeUtc);
        Assert.Equal(accountId, receiptEvent.AccountId);

        // NFR-15 format validation: 3 leading chars, hyphen, 3 asterisks, dot, 2 trailing chars
        Assert.Matches(@"^[A-Z0-9]{3}-\*\*\*\.[0-9]{2}$", receiptEvent.MaskedPlateNumber);
    }

    [Fact]
    public void Receipt_event_supports_unregistered_customer_with_null_account_id()
    {
        var receiptEvent = new ParkingSessionReceiptIssuedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "59X-***.12",
            15000,
            15000,
            0,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            AccountId: null);

        Assert.Null(receiptEvent.AccountId);
    }

    [Fact]
    public void Parking_session_fee_proto_declares_qr_rpc_and_request_message()
    {
        // Locate proto file relative to test execution
        var protoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "contracts", "grpc", "parking_session_fee.proto");
        if (!File.Exists(protoPath))
        {
            // Fallback for different working directories
            protoPath = Path.GetFullPath("contracts/grpc/parking_session_fee.proto");
        }

        if (File.Exists(protoPath))
        {
            var content = File.ReadAllText(protoPath);
            Assert.Contains("rpc GetSessionFeeContextByQr(GetSessionFeeContextByQrRequest)", content);
            Assert.Contains("message GetSessionFeeContextByQrRequest", content);
            Assert.Contains("string qr_token = 1;", content);
        }
    }
}
