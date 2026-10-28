using System;

namespace ParkingSystem.Payment.Domain.Pricing;

// Tính tiền đỗ xe từ lúc vào đến giờ
public static class ParkingFeeCalculator
{
    public static ParkingFee Calculate(
        ParkingMethod method,                 // Cách tính: theo lượt hay theo khoảng thời gian
        int blockMinutes,                     // Số phút của một khoảng tính tiền
        long pricePerUnit,                    // Giá tiền một lượt hoặc một khoảng tính
        DateTimeOffset entryTimeUtc,          // Giờ xe vào bãi theo giờ quốc tế
        DateTimeOffset calculatedAtUtc,       // Giờ tính tiền hiện tại theo giờ quốc tế
        long overnightSurcharge = 0,          // Tiền phụ thu qua đêm, mặc định không có
        TimeSpan overnightTimeMark = default) // Mốc giờ bắt đầu tính tiền qua đêm
    {
        // Lấy số phút đỗ, làm tròn lên và chặn không cho âm
        var durationMinutes = Math.Max(0,
            (int)Math.Ceiling((calculatedAtUtc - entryTimeUtc).TotalMinutes));

        // Chọn mốc giờ kết thúc hợp lệ để tránh lỗi
        var effectiveEnd = calculatedAtUtc < entryTimeUtc
            ? entryTimeUtc
            : calculatedAtUtc;

        var baseFee = PricingEngine.CalculateBaseFee(
            entryTimeUtc,
            effectiveEnd,
            method,
            blockMinutes,
            pricePerUnit,
            overnightSurcharge,
            overnightTimeMark);

        // Ép sang số nguyên, báo lỗi nếu tiền vượt giới hạn
        var totalFee = checked((long)baseFee);

        return new ParkingFee(durationMinutes, totalFee);
    }
}
