using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Domain.Entities;

// Kiem tra khoi tao hoa don thu tien mat
public sealed class ParkingFeePaymentCashTests
{
    [Fact]
    public void CreateCashPayment_ShouldInitializeCashPayment_WithSucceededStatusAndNullWallet()
    {
        // Du lieu dau vao hop le
        var sessionId = Guid.NewGuid();
        var parkingLotId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var plateNumber = "51A-12345";
        var vehicleType = "Car";
        long totalFee = 50000;
        long prepaidAmount = 10000;
        long amountPaid = 40000;
        var paymentTimeUtc = DateTimeOffset.UtcNow;
        var exitWindowExpiresUtc = paymentTimeUtc.AddMinutes(15);

        // Tao hoa don thu tien mat
        var payment = ParkingFeePayment.CreateCashPayment(
            sessionId,
            parkingLotId,
            accountId,
            plateNumber,
            vehicleType,
            totalFee,
            prepaidAmount,
            amountPaid,
            paymentTimeUtc,
            exitWindowExpiresUtc);

        // Kiem tra thong tin hoa don
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(sessionId, payment.SessionId);
        Assert.Equal(parkingLotId, payment.ParkingLotId);
        Assert.Equal(accountId, payment.AccountId);
        Assert.Null(payment.WalletId);
        Assert.Equal(plateNumber, payment.PlateNumber);
        Assert.Equal(vehicleType, payment.VehicleType);
        Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
        Assert.Equal(totalFee, payment.TotalFee);
        Assert.Equal(prepaidAmount, payment.PrepaidAmount);
        Assert.Equal(amountPaid, payment.AmountPaid);
        Assert.Equal(paymentTimeUtc, payment.PaymentTimeUtc);
        Assert.Equal(exitWindowExpiresUtc, payment.ExitWindowExpiresUtc);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(paymentTimeUtc, payment.ConfirmedAtUtc);
    }

    [Fact]
    public void CreateCashPayment_WithNullAccountId_ForUnregisteredGuest_ShouldSucceed()
    {
        // Khach vang lai khong co tai khoan
        var sessionId = Guid.NewGuid();
        var parkingLotId = Guid.NewGuid();
        var plateNumber = "59B-99999";
        var vehicleType = "Motorbike";
        long totalFee = 10000;
        long prepaidAmount = 0;
        long amountPaid = 10000;
        var paymentTimeUtc = DateTimeOffset.UtcNow;
        var exitWindowExpiresUtc = paymentTimeUtc.AddMinutes(15);

        // Tao hoa don cho khach vang lai
        var payment = ParkingFeePayment.CreateCashPayment(
            sessionId,
            parkingLotId,
            null,
            plateNumber,
            vehicleType,
            totalFee,
            prepaidAmount,
            amountPaid,
            paymentTimeUtc,
            exitWindowExpiresUtc);

        // Kiem tra accountId la null va trang thai thanh cong
        Assert.Null(payment.AccountId);
        Assert.Null(payment.WalletId);
        Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(amountPaid, payment.AmountPaid);
        Assert.Equal(paymentTimeUtc, payment.ConfirmedAtUtc);
    }
}
