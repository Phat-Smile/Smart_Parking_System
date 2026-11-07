using System;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Pricing;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Pricing;

public class PricingEngineTests
{
    private readonly TimeSpan _vnOffset = TimeSpan.FromHours(7); 
    private readonly TimeSpan _overnightMark = new TimeSpan(22, 0, 0); // Mốc 22:00
    private readonly TimeSpan _midnightMark = new TimeSpan(0, 0, 0);   // Mốc mặc định 00:00
    private readonly decimal _pricePerBlock = 10000m;
    private readonly decimal _pricePerTurn = 5000m;
    private readonly decimal _overnightFee = 50000m;

    [Fact]
    public void CalculateBaseFee_PerBlock_ShouldRoundUpCorrectly()
    {
        // Khách đỗ 65 phút, bãi tính block 30 phút -> 3 block (30k)
        var start = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 10, 11, 5, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark);

        Assert.Equal(30000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_PerTurn_CrossesOvernight_ShouldAddOvernightFee()
    {
        // Đỗ theo lượt từ 20:00 đến 07:00 sáng hôm sau qua mốc 22:00 -> 1 lượt (5k) + 1 đêm (50k) = 55k
        var start = new DateTimeOffset(2026, 10, 10, 20, 0, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 11, 7, 0, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerTurn, 30, _pricePerTurn, _overnightFee, _overnightMark);

        Assert.Equal(55000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_EndTimeBeforeStartTime_ShouldThrowArgumentException()
    {
        // Giờ ra nhỏ hơn giờ vào -> ném ArgumentException
        var start = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 10, 9, 0, 0, _vnOffset);

        var exception = Assert.Throws<ArgumentException>(() =>
            PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark));

        Assert.Contains(DomainErrorMessages.Pricing.EndTimeBeforeStartTime, exception.Message);
        Assert.Equal("endTime", exception.ParamName);
    }

    [Fact]
    public void CalculateBaseFee_EndTimeEqualsStartTime_ShouldChargeMinimumOneBlock()
    {
        // Vừa vào tính phí ngay: tính tối thiểu 1 block theo BR-09.a
        var time = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(time, time, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark);

        Assert.Equal(10000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_ShortParking_CrossesMidnight_ShouldAddOvernightFee()
    {
        // Đỗ ngắn 20 phút (23:50 -> 00:10 hôm sau), nhưng vắt qua mốc 00:00 -> có mặt trong bãi lúc 00:00 -> tính phí đêm (BR-09.b)
        var start = new DateTimeOffset(2026, 10, 10, 23, 50, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 11, 0, 10, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _midnightMark);

        // 20 phút = 1 block (10k) + 1 đêm (50k) = 60k
        Assert.Equal(60000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_ShortParking_BeforeMidnight_ShouldNotAddOvernightFee()
    {
        // Đỗ ngắn 25 phút (23:30 -> 23:55), ra trước 00:00 -> không tính phí đêm
        var start = new DateTimeOffset(2026, 10, 10, 23, 30, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 10, 23, 55, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _midnightMark);

        // 25 phút = 1 block (10k), 0 đêm = 10k
        Assert.Equal(10000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_ShortParking_AfterMidnight_ShouldNotAddOvernightFee()
    {
        // Vào lúc 00:05, ra lúc 00:30 (sau mốc 00:00) -> không vắt qua mốc -> không tính phí đêm
        var start = new DateTimeOffset(2026, 10, 11, 0, 5, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 11, 0, 30, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _midnightMark);

        Assert.Equal(10000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_MultipleNights_ShouldChargeMultipleOvernightFees()
    {
        // Đỗ 2 ngày liên tiếp qua 2 mốc 00:00 (từ 10:00 ngày 10/10 đến 10:00 ngày 12/10)
        var start = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 12, 10, 0, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerTurn, 30, _pricePerTurn, _overnightFee, _midnightMark);

        // 1 lượt (5k) + 2 đêm (2 * 50k = 100k) = 105k
        Assert.Equal(105000m, fee);
    }

    [Fact]
    public void CalculateBaseFee_UtcInputs_ShouldConvertCorrectlyToVietnamOffset()
    {
        // Truyền thời gian dạng UTC: 10/10 16:50 UTC (tức 23:50 VN) đến 10/10 17:10 UTC (tức 00:10 VN ngày 11/10)
        var startUtc = new DateTimeOffset(2026, 10, 10, 16, 50, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 10, 10, 17, 10, 0, TimeSpan.Zero);

        decimal fee = PricingEngine.CalculateBaseFee(startUtc, endUtc, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _midnightMark);

        // Vắt qua 00:00 giờ VN -> tính 1 đêm (50k) + 1 block (10k) = 60k
        Assert.Equal(60000m, fee);
    }

    [Fact]
    public void CalculateOverstayFee_ExitWithinGracePeriod_ShouldReturnZero()
    {
        // Thanh toán 10:00, ra lúc 10:15 (vừa sát mốc 15 phút ân hạn) -> 0đ
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 10, 15, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateOverstayFee(paymentTime, exitTime, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateOverstayFee_ExitLate_PerBlock_ShouldChargeBlocks()
    {
        // Thanh toán 10:00, ân hạn đến 10:15. Ra lúc 10:20 (lố 5 phút) -> 1 block (10k)
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 10, 20, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateOverstayFee(paymentTime, exitTime, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark);

        Assert.Equal(10000m, fee);
    }

    [Fact]
    public void CalculateOverstayFee_ExitLate_PerTurn_ShouldNotChargeTurnAgain()
    {
        // Bãi theo lượt: ra lố giờ không tính thêm lượt, chỉ tính phạt đêm nếu có
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 10, 20, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateOverstayFee(paymentTime, exitTime, ParkingMethod.PerTurn, 30, _pricePerTurn, _overnightFee, _overnightMark);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateOverstayFee_CustomGracePeriod_ShouldRespectConfiguredValue()
    {
        // Cấu hình bãi xe có ân hạn 20 phút thay vì 15 phút. Thanh toán 10:00, ra 10:18 -> vẫn trong ân hạn -> 0đ
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 10, 18, 0, _vnOffset);

        decimal fee = PricingEngine.CalculateOverstayFee(
            paymentTime,
            exitTime,
            ParkingMethod.PerBlock,
            30,
            _pricePerBlock,
            _overnightFee,
            _overnightMark,
            gracePeriodMinutes: 20);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateOverstayFee_ExitBeforePaymentTime_ShouldThrowArgumentException()
    {
        // Giờ ra trước giờ thanh toán -> dữ liệu bất thường -> ném ngoại lệ
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 9, 50, 0, _vnOffset);

        var exception = Assert.Throws<ArgumentException>(() =>
            PricingEngine.CalculateOverstayFee(paymentTime, exitTime, ParkingMethod.PerBlock, 30, _pricePerBlock, _overnightFee, _overnightMark));

        Assert.Contains(DomainErrorMessages.Pricing.ExitTimeBeforePaymentTime, exception.Message);
        Assert.Equal("exitTime", exception.ParamName);
    }

    [Fact]
    public void CalculateBaseFee_PerBlock_ZeroBlockMinutes_ShouldThrowArgumentException()
    {
        var start = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var end = new DateTimeOffset(2026, 10, 10, 11, 0, 0, _vnOffset);

        var exception = Assert.Throws<ArgumentException>(() =>
            PricingEngine.CalculateBaseFee(start, end, ParkingMethod.PerBlock, 0, _pricePerBlock, _overnightFee, _overnightMark));

        Assert.Contains(DomainErrorMessages.Pricing.BlockMinutesMustBePositive, exception.Message);
        Assert.Equal("blockMinutes", exception.ParamName);
    }

    [Fact]
    public void CalculateOverstayFee_NegativeGracePeriod_ShouldThrowArgumentOutOfRangeException()
    {
        var paymentTime = new DateTimeOffset(2026, 10, 10, 10, 0, 0, _vnOffset);
        var exitTime = new DateTimeOffset(2026, 10, 10, 10, 20, 0, _vnOffset);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PricingEngine.CalculateOverstayFee(
                paymentTime,
                exitTime,
                ParkingMethod.PerBlock,
                30,
                _pricePerBlock,
                _overnightFee,
                _overnightMark,
                gracePeriodMinutes: -5));

        Assert.Contains(DomainErrorMessages.Pricing.GracePeriodMinutesNegative, exception.Message);
        Assert.Equal("gracePeriodMinutes", exception.ParamName);
    }

    [Theory]
    [InlineData(2026, 11, 6, 8, 0, true)]   // Friday 08:00 (Morning Peak)
    [InlineData(2026, 11, 6, 18, 0, true)]  // Friday 18:00 (Evening Peak)
    [InlineData(2026, 11, 6, 12, 0, false)] // Friday 12:00 (Off-peak midday)
    [InlineData(2026, 11, 7, 8, 0, false)]  // Saturday 08:00 (Weekend - no surge)
    [InlineData(2026, 11, 8, 18, 0, false)] // Sunday 18:00 (Weekend - no surge)
    public void IsPeakHour_ShouldIdentifyPeakWindowCorrectly(int year, int month, int day, int hour, int minute, bool expected)
    {
        var testTime = new DateTimeOffset(year, month, day, hour, minute, 0, _vnOffset);
        var isPeak = PricingEngine.IsPeakHour(testTime, _vnOffset);
        Assert.Equal(expected, isPeak);
    }

    [Fact]
    public void CalculateSurgeMultiplier_PeakHour_ShouldApply1_2x()
    {
        var peakTime = new DateTimeOffset(2026, 11, 6, 8, 15, 0, _vnOffset); // Friday 08:15
        var multiplier = PricingEngine.CalculateSurgeMultiplier(peakTime, occupancyRate: 0.5, localTimeZoneOffset: _vnOffset);
        Assert.Equal(1.2m, multiplier);
    }

    [Fact]
    public void CalculateSurgeMultiplier_HighOccupancy_ShouldApply1_3x()
    {
        var offPeakTime = new DateTimeOffset(2026, 11, 6, 14, 0, 0, _vnOffset); // Friday 14:00
        var multiplier = PricingEngine.CalculateSurgeMultiplier(offPeakTime, occupancyRate: 0.90, localTimeZoneOffset: _vnOffset);
        Assert.Equal(1.3m, multiplier);
    }

    [Fact]
    public void CalculateWithSurge_PeakHour_ShouldIncreaseFeeBy20Percent()
    {
        var entryTime = new DateTimeOffset(2026, 11, 6, 7, 0, 0, _vnOffset);
        var calcTime = new DateTimeOffset(2026, 11, 6, 8, 0, 0, _vnOffset); // 60 minutes = 2 blocks of 30m
        // 2 blocks * 10,000 = 20,000 base fee. Surge 1.2x -> 24,000
        var fee = ParkingFeeCalculator.CalculateWithSurge(
            ParkingMethod.PerBlock,
            30,
            10000,
            entryTime,
            calcTime,
            occupancyRate: 0.5);

        Assert.Equal(60, fee.DurationMinutes);
        Assert.Equal(24000, fee.TotalFee);
    }
}
