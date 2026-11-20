using System;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Persistence.Repositories;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Repositories;

public sealed class PrepaymentReaderTests
{
    [Fact]
    public void CalculateNetPrepaid_WhenDepositForfeited_ReturnsZero()
    {
        var entries = new[]
        {
            (LedgerEntryType.ReservationDeposit, -20_000m),
            (LedgerEntryType.PrepaymentForfeited, 20_000m)
        };

        var result = PrepaymentReader.CalculateNetPrepaid(entries);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void CalculateNetPrepaid_WhenDepositApplied_ReturnsZero()
    {
        var entries = new[]
        {
            (LedgerEntryType.ReservationDeposit, -20_000m),
            (LedgerEntryType.PrepaymentApplied, 20_000m)
        };

        var result = PrepaymentReader.CalculateNetPrepaid(entries);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void CalculateNetPrepaid_WhenDepositOnly_ReturnsDepositAmount()
    {
        var entries = new[]
        {
            (LedgerEntryType.ReservationDeposit, -20_000m)
        };

        var result = PrepaymentReader.CalculateNetPrepaid(entries);

        Assert.Equal(20_000L, result);
    }

    [Fact]
    public void CalculateNetPrepaid_WhenPartialRefund_ReturnsRemainingAmount()
    {
        var entries = new[]
        {
            (LedgerEntryType.ReservationDeposit, -60_000m),
            (LedgerEntryType.Refund, 20_000m)
        };

        var result = PrepaymentReader.CalculateNetPrepaid(entries);

        Assert.Equal(40_000L, result);
    }
}
