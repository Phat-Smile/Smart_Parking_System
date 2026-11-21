using System.Text.RegularExpressions;
using ParkingSystem.Contracts.Events.Payment;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Contracts;

public sealed class ElectronicInvoiceIssuedContractTests
{
    [Fact]
    public void Electronic_invoice_event_holds_expected_contract_fields_and_anonymized_plate()
    {
        var sessionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var paymentTime = DateTimeOffset.UtcNow;
        var exitExpires = paymentTime.AddMinutes(15);
        const string maskedPlate = "51F-***.89";
        const long totalFee = 30000;
        const long paidAmount = 30000;
        const long amountDue = 0;

        var invoiceEvent = new ElectronicInvoiceIssuedIntegrationEvent(
            sessionId,
            paymentId,
            lotId,
            maskedPlate,
            totalFee,
            paidAmount,
            amountDue,
            paymentTime,
            exitExpires,
            accountId);

        Assert.Equal(sessionId, invoiceEvent.SessionId);
        Assert.Equal(paymentId, invoiceEvent.PaymentId);
        Assert.Equal(lotId, invoiceEvent.ParkingLotId);
        Assert.Equal(maskedPlate, invoiceEvent.MaskedPlateNumber);
        Assert.Equal(totalFee, invoiceEvent.TotalFee);
        Assert.Equal(paidAmount, invoiceEvent.PaidAmount);
        Assert.Equal(amountDue, invoiceEvent.AmountDue);
        Assert.Equal(paymentTime, invoiceEvent.PaymentTimeUtc);
        Assert.Equal(exitExpires, invoiceEvent.ExitWindowExpiresUtc);
        Assert.Equal(accountId, invoiceEvent.AccountId);

        // NFR-15 format validation: 3 leading chars, hyphen, 3 asterisks, dot, 2 trailing chars
        Assert.Matches(@"^[A-Z0-9]{3}-\*\*\*\.[0-9]{2}$", invoiceEvent.MaskedPlateNumber);
    }

    [Fact]
    public void Electronic_invoice_event_supports_unregistered_customer_with_null_account_id()
    {
        var invoiceEvent = new ElectronicInvoiceIssuedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "59X-***.12",
            15000,
            15000,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15),
            AccountId: null);

        Assert.Null(invoiceEvent.AccountId);
    }
}
