namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;

// Kết quả xem trước phí đỗ xe
public sealed record ParkingSessionFeeResponse(
    Guid SessionId,                 // Mã phiên đỗ xe
    string PlateNumber,             // Biển số xe
    string VehicleType,             // Loại xe
    DateTimeOffset EntryTimeUtc,    // Giờ xe vào bãi theo giờ quốc tế
    DateTimeOffset CalculatedAtUtc, // Giờ tính tiền hiện tại theo giờ quốc tế
    int DurationMinutes,            // Số phút đỗ xe đã làm tròn lên
    string PricingPolicyId,         // Mã bảng giá áp dụng
    string CalculationMethod,       // Cách tính: theo lượt hay theo khoảng thời gian
    long TotalFee,                  // Tổng tiền đỗ xe thực tế
    long DepositAmount,             // Tiền đã cọc trước nếu có đặt chỗ
    long AmountDue);                // Tiền còn phải trả sau khi trừ cọc
