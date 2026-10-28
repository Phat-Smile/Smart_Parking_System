namespace ParkingSystem.Payment.Domain.Pricing;

// Lưu kết quả tính phí đỗ xe
public sealed record ParkingFee(int DurationMinutes, long TotalFee);
