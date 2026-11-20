using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Entities;

public class ParkingFeePaymentTests
{
    [Fact]
    public void FactoryMethods_DefaultSequence_ShouldBeOne()
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(15);

        var walletPayment = ParkingFeePayment.CreateWalletPayment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "51A-12345", "Car", 20000, 0, 20000, now, expires);

        var cashPayment = ParkingFeePayment.CreateCashPayment(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "51A-12346", "Car", 15000, 0, 15000, now, expires);

        var vnpayPayment = ParkingFeePayment.CreateVnPayPayment(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "51A-12347", "Car", 30000, 0, 30000, now, expires, "TXN_1", "https://sandbox.vnpayment.vn");

        Assert.Equal(1, walletPayment.PaymentSequence);
        Assert.Equal(1, cashPayment.PaymentSequence);
        Assert.Equal(1, vnpayPayment.PaymentSequence);
    }

    [Fact]
    public void OverstaySequence_CreatesNewImmutableRecord_WithoutChangingOriginal()
    {
        var sessionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = ParkingFeePayment.CreateCashPayment(
            sessionId, lotId, null, "51A-12345", "Car", 20000, 0, 20000, now, now.AddMinutes(15));

        var second = ParkingFeePayment.CreateCashPayment(
            sessionId, lotId, null, "51A-12345", "Car", 10000, 0, 10000, now.AddMinutes(30), now.AddMinutes(45),
            paymentSequence: 2);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.PaymentSequence);
        Assert.Equal(20000, first.TotalFee);
        Assert.Equal(20000, first.AmountPaid);
        Assert.Equal(2, second.PaymentSequence);
        Assert.Equal(10000, second.AmountPaid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateWithInvalidSequence_ShouldThrowArgumentOutOfRangeException(int invalidSequence)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() => ParkingFeePayment.CreateWalletPayment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "51A-12345", "Car", 20000, 0, 20000, now, now.AddMinutes(15), invalidSequence));

        Assert.Throws<ArgumentOutOfRangeException>(() => ParkingFeePayment.CreateCashPayment(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "51A-12345", "Car", 20000, 0, 20000, now, now.AddMinutes(15), invalidSequence));

        Assert.Throws<ArgumentOutOfRangeException>(() => ParkingFeePayment.CreateVnPayPayment(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "51A-12345", "Car", 20000, 0, 20000, now, now.AddMinutes(15), "TXN_INVALID", "https://sandbox.vnpayment.vn", invalidSequence));
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Surplus)]
    public void MarkAsSucceeded_WhenAlreadyTerminal_ShouldThrowInvalidOperationException(string terminalStatus)
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "51A-12345", "Car", 20000, 0, 20000, now, now.AddMinutes(15), "TXN_TERM", "https://sandbox.vnpayment.vn");

        switch (terminalStatus)
        {
            case PaymentStatus.Succeeded:
                payment.MarkAsSucceeded("GW1", now, now.AddMinutes(15));
                break;
            case PaymentStatus.Failed:
                payment.MarkAsFailed("24", now);
                break;
            case PaymentStatus.Surplus:
                payment.MarkAsSurplus("GW2", "NeedsManualRefund", now);
                break;
        }

        Assert.Throws<InvalidOperationException>(() => payment.MarkAsSucceeded("GW_LATE", now.AddMinutes(1), now.AddMinutes(16)));
    }
}
