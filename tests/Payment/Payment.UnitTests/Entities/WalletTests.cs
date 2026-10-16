using System;
using Xunit;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Tests.Payment.UnitTests.Entities;

public class WalletTests
{
    // 1. Test khởi tạo và đóng ví

    [Fact]
    public void Create_NewWallet_ShouldHaveZeroBalance_Active_AndInitialVersionOne()
    {
        // Arrange & Act
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);

        // Assert
        Assert.Equal(0m, wallet.Balance);
        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Equal(1L, wallet.Version);
    }

    [Fact]
    public void Close_WhenBalanceNotZero_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 10000m);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Close());
        Assert.Equal(DomainErrorMessages.Wallet.MustBeZeroToClose, ex.Message);
    }

    [Fact]
    public void Close_WhenBalanceIsZero_ShouldSucceed_AndIncrementVersion()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 10000m);
        wallet.AddEntry(LedgerEntryType.FeePayment, -10000m);
        long versionBeforeClose = wallet.Version;

        // Act
        wallet.Close();

        // Assert
        Assert.Equal(WalletStatus.Closed, wallet.Status);
        Assert.NotNull(wallet.ClosedAt);
        Assert.Equal(versionBeforeClose + 1, wallet.Version);
    }

    [Fact]
    public void Close_AlreadyClosedWallet_ShouldDoNothing()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.Close();
        long versionAfterFirstClose = wallet.Version;

        // Act
        wallet.Close();

        // Assert
        Assert.Equal(versionAfterFirstClose, wallet.Version);
    }

    [Fact]
    public void AddEntry_ToClosedWallet_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.Close();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            wallet.AddEntry(LedgerEntryType.TopUp, 50000m));

        Assert.Equal(DomainErrorMessages.Wallet.AlreadyClosed, ex.Message);
    }

    // 2. Test dấu tiền và tính toàn vẹn tiền tệ (VND)

    [Fact]
    public void AddEntry_AmountIsZero_ShouldThrowArgumentException()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.Adjustment, 0m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountCannotBeZero, ex.Message);
    }

    [Fact]
    public void AddEntry_FractionalAmount_ShouldThrowArgumentException()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.TopUp, 10000.5m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBeInteger, ex.Message);
    }

    [Fact]
    public void AddEntry_ValidTopUp_ShouldIncreaseBalance_AndIncrementVersion()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());

        // Act
        wallet.AddEntry(LedgerEntryType.TopUp, 100000m);

        // Assert
        Assert.Equal(100000m, wallet.Balance);
        Assert.Equal(2L, wallet.Version);
        Assert.Single(wallet.LedgerEntries);
    }

    [Fact]
    public void AddEntry_TopUpWithNegativeAmount_ShouldThrowArgumentException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.TopUp, -50000m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBePositive, ex.Message);
    }

    [Fact]
    public void AddEntry_RefundWithNegativeAmount_ShouldThrowArgumentException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.Refund, -50000m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBePositive, ex.Message);
    }

    [Fact]
    public void AddEntry_FeePaymentWithPositiveAmount_ShouldThrowArgumentException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.FeePayment, 20000m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBeNegative, ex.Message);
    }

    [Fact]
    public void AddEntry_PrepaymentWithPositiveAmount_ShouldThrowArgumentException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        var ex = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.Prepayment, 20000m));

        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBeNegative, ex.Message);
    }

    [Fact]
    public void AddEntry_NotEnoughMoney_ShouldThrowInvalidOperationException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 10000m);

        // Cố tình trừ 50k trong khi ví chỉ có 10k
        var ex = Assert.Throws<InvalidOperationException>(() =>
            wallet.AddEntry(LedgerEntryType.FeePayment, -50000m));

        Assert.Equal(DomainErrorMessages.Wallet.NotEnoughMoney, ex.Message);
    }

    // 3. Test nghiệp vụ tiền cọc (BR-12.c)

    [Fact]
    public void AddEntry_PrepaymentApplied_ShouldNotChangeBalance_AndKeepAmountPositive()
    {
        // Arrange: Nạp 100k, sau đó đặt chỗ trừ cọc 20k -> Số dư còn 80k
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 100000m);
        wallet.AddEntry(LedgerEntryType.Prepayment, -20000m);
        Assert.Equal(80000m, wallet.Balance);

        // Act: Xe vào bãi, áp cọc 20k thành phí đỗ xe
        wallet.AddEntry(LedgerEntryType.PrepaymentApplied, 20000m);

        // Assert: Số dư ví vẫn phải là 80k
        Assert.Equal(80000m, wallet.Balance);
        Assert.Equal(4L, wallet.Version);
        Assert.Equal(3, wallet.LedgerEntries.Count);
    }

    [Fact]
    public void AddEntry_PrepaymentForfeited_ShouldNotChangeBalance_AndKeepAmountPositive()
    {
        // Arrange: Nạp 100k, sau đó đặt chỗ trừ cọc 20k -> Số dư còn 80k
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 100000m);
        wallet.AddEntry(LedgerEntryType.Prepayment, -20000m);
        Assert.Equal(80000m, wallet.Balance);

        // Act: Khách không đến, tịch thu cọc 20k
        wallet.AddEntry(LedgerEntryType.PrepaymentForfeited, 20000m);

        // Assert: Số dư ví vẫn là 80k (không trừ đúp)
        Assert.Equal(80000m, wallet.Balance);
        Assert.Equal(4L, wallet.Version);
        Assert.Equal(3, wallet.LedgerEntries.Count);
    }

    [Fact]
    public void AddEntry_PrepaymentAppliedOrForfeited_WithNegativeAmount_ShouldThrowArgumentException()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        var ex1 = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.PrepaymentApplied, -20000m));
        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBePositive, ex1.Message);

        var ex2 = Assert.Throws<ArgumentException>(() =>
            wallet.AddEntry(LedgerEntryType.PrepaymentForfeited, -20000m));
        Assert.Contains(DomainErrorMessages.Wallet.AmountMustBePositive, ex2.Message);
    }

    [Fact]
    public void AddEntry_Adjustment_ShouldUpdateBalanceCorrectly()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddEntry(LedgerEntryType.TopUp, 50000m);

        // Act: Kế toán điều chỉnh cộng thêm 10k
        wallet.AddEntry(LedgerEntryType.Adjustment, 10000m, "System error adjustment credit");
        Assert.Equal(60000m, wallet.Balance);

        // Act: Kế toán điều chỉnh trừ bớt 20k
        wallet.AddEntry(LedgerEntryType.Adjustment, -20000m, "Erroneous fee refund adjustment");
        Assert.Equal(40000m, wallet.Balance);
    }

    [Fact]
    public void AddEntry_WithUndefinedLedgerEntryType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        var undefinedType = (LedgerEntryType)999;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            wallet.AddEntry(undefinedType, 10000m));
        Assert.Contains("does not have balance rules configured", ex.Message);
    }
}
