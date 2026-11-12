namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Lưu bảng giá tại thời điểm xe bắt đầu vào bãi
public sealed record PricingSnapshotDto(
    string PolicyId,            // Mã định danh của bảng giá áp dụng
    string CalculationMethod,   // Cách tính: theo khoảng thời gian hay theo lượt
    int BlockMinutes,           // Số phút trong một khoảng tính tiền
    long PricePerUnit);         // Số tiền của một lượt hoặc một khoảng tính

// Lưu thông tin chi tiết phiên xe do bãi gửi sang
public sealed record ParkingSessionFeeContext(
    Guid SessionId,             // Mã phiên đỗ xe
    Guid ParkingLotId,          // Mã của bãi giữ xe
    string PlateNumber,         // Biển số xe đỗ trong bãi
    string VehicleType,         // Loại xe: xe hơi hoặc xe máy
    string Status,              // Tình trạng: đang đỗ, đã trả tiền, hoặc đã ra bãi
    DateTimeOffset EntryTimeUtc,// Giờ xe vào bãi theo giờ quốc tế
    Guid? AccountId,            // Mã tài khoản người gửi xe, có thể để trống
    Guid? BookingId,            // Mã đặt chỗ trước, có thể để trống nếu không đặt
    PricingSnapshotDto Pricing);// Bảng giá gắn liền với phiên gửi xe này
