namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng đọc tiền cọc từ sổ cái
public interface IPrepaymentReader
{
    // Đọc số tiền cọc trước của lượt đặt chỗ
    Task<long> GetPrepaidAmountAsync(
        Guid accountId,                                 // Mã tài khoản người gửi xe
        Guid bookingId,                                 // Mã đặt chỗ trước
        CancellationToken cancellationToken = default); // Tín hiệu hủy lệnh nếu người dùng ngắt kết nối
}
