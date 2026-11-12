namespace ParkingSystem.Payment.Domain.Constants;

// Quản lý tập trung các trạng thái của hóa đơn tiền đỗ xe
public static class PaymentStatus
{
    public const string Pending = "Pending";       // Đang chờ quét mã thanh toán
    public const string Superseded = "Superseded"; // Đã bị thay thế bởi mã mới
    public const string Succeeded = "Succeeded";   // Đã thanh toán thành công
    public const string Failed = "Failed";         // Thanh toán thất bại
    public const string Expired = "Expired";       // Quá hạn không thanh toán
    public const string Surplus = "Surplus";       // Thu tiền thừa do xe đã trả trước
}
