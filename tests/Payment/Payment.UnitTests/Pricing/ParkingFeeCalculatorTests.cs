using System;
using ParkingSystem.Payment.Domain.Pricing;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Pricing;

public class ParkingFeeCalculatorTests
{
    private readonly DateTimeOffset _entryTime = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_PerEntry_ReturnsFlatFee()
    {
        // TC-FEE-01: Tính phí theo lượt, đỗ 3 tiếng tính đúng giá 1 lượt
        var calculatedAt = _entryTime.AddHours(3);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerTurn,
            blockMinutes: 30,
            pricePerUnit: 10000,
            _entryTime,
            calculatedAt);

        Assert.Equal(180, result.DurationMinutes);
        Assert.Equal(10000, result.TotalFee);
    }

    [Fact]
    public void Calculate_BlockFee_ExactMultiple_CalculatesCorrectBlocks()
    {
        // TC-FEE-02: Đỗ đúng 60 phút với block 30 phút -> tính 2 block
        var calculatedAt = _entryTime.AddMinutes(60);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: 30,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(60, result.DurationMinutes);
        Assert.Equal(16000, result.TotalFee);
    }

    [Fact]
    public void Calculate_BlockFee_RoundsUpToNextBlock()
    {
        // TC-FEE-03: Đỗ 80 phút với block 30 phút -> làm tròn lên 3 block
        var calculatedAt = _entryTime.AddMinutes(80);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: 30,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(80, result.DurationMinutes);
        Assert.Equal(24000, result.TotalFee);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Calculate_BlockFee_MinimumOneBlockCharged(int minutes)
    {
        // TC-FEE-04: Đỗ 0 phút hoặc 5 phút đều tính tối thiểu 1 block
        var calculatedAt = _entryTime.AddMinutes(minutes);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: 30,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(minutes, result.DurationMinutes);
        Assert.Equal(8000, result.TotalFee);
    }

    [Fact]
    public void Calculate_RoundingUpOfSeconds_IncreasesDurationByOneMinute()
    {
        // TC-FEE-05: Đỗ 60 phút 30 giây -> làm tròn thành 61 phút (3 block)
        var calculatedAt = _entryTime.AddMinutes(60).AddSeconds(30);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: 30,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(61, result.DurationMinutes);
        Assert.Equal(24000, result.TotalFee);
    }

    [Theory]
    [InlineData(15, 72000)]
    [InlineData(30, 40000)]
    [InlineData(60, 24000)]
    public void Calculate_DifferentBlockSizes_CalculatesExpectedUnits(int blockSize, long expectedFee)
    {
        // TC-FEE-06: 135 phút với các kích thước block 15/30/60 phút
        var calculatedAt = _entryTime.AddMinutes(135);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: blockSize,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(135, result.DurationMinutes);
        Assert.Equal(expectedFee, result.TotalFee);
    }

    [Fact]
    public void Calculate_ClockSkew_ClampsDurationToZeroAndChargesOneUnit()
    {
        // TC-FEE-13: Lệch giờ (giờ tính nhỏ hơn giờ vào) -> thời gian về 0, tính 1 lượt
        var calculatedAt = _entryTime.AddMinutes(-5);
        var result = ParkingFeeCalculator.Calculate(
            ParkingMethod.PerBlock,
            blockMinutes: 30,
            pricePerUnit: 8000,
            _entryTime,
            calculatedAt);

        Assert.Equal(0, result.DurationMinutes);
        Assert.Equal(8000, result.TotalFee);
    }

    [Fact]
    public void Calculate_MoneyOverflow_ThrowsOverflowException()
    {
        // TC-FEE-29: Tiền vượt giới hạn long -> ném ngoại lệ OverflowException
        var calculatedAt = _entryTime.AddHours(100);
        Assert.Throws<OverflowException>(() =>
        {
            ParkingFeeCalculator.Calculate(
                ParkingMethod.PerBlock,
                blockMinutes: 1,
                pricePerUnit: long.MaxValue / 10,
                _entryTime,
                calculatedAt);
        });
    }
}
