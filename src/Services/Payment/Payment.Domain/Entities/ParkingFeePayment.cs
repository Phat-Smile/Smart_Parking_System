namespace ParkingSystem.Payment.Domain.Entities;

// Hóa đơn thanh toán tiền gửi xe
public class ParkingFeePayment
{
    public Guid Id { get; private set; }                     // Mã hóa đơn thanh toán
    public Guid SessionId { get; private set; }              // Mã phiên đỗ xe
    public Guid ParkingLotId { get; private set; }           // Mã bãi giữ xe
    public Guid WalletId { get; private set; }               // Mã ví đã trừ tiền
    public Guid AccountId { get; private set; }              // Mã người thanh toán
    public string PlateNumber { get; private set; } = null!; // Biển số xe gửi
    public string VehicleType { get; private set; } = null!; // Loại xe gửi
    public long TotalFee { get; private set; }               // Tổng tiền đỗ xe
    public long PrepaidAmount { get; private set; }          // Tiền cọc đặt chỗ được trừ
    public long AmountPaid { get; private set; }             // Số tiền thực trừ từ ví
    public DateTimeOffset PaymentTimeUtc { get; private set; }// Giờ thanh toán xong
    public DateTimeOffset ExitWindowExpiresUtc { get; private set; } // Giờ hết hạn ra bãi
    public string Status { get; private set; } = null!;      // Tình trạng: Succeeded

    private ParkingFeePayment() { }

    // Tạo mới một hóa đơn thanh toán phí đỗ
    public static ParkingFeePayment Create(
        Guid sessionId,
        Guid parkingLotId,
        Guid walletId,
        Guid accountId,
        string plateNumber,
        string vehicleType,
        long totalFee,
        long prepaidAmount,
        long amountPaid,
        DateTimeOffset paymentTimeUtc,
        DateTimeOffset exitWindowExpiresUtc)
    {
        return new ParkingFeePayment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParkingLotId = parkingLotId,
            WalletId = walletId,
            AccountId = accountId,
            PlateNumber = plateNumber,
            VehicleType = vehicleType,
            TotalFee = totalFee,
            PrepaidAmount = prepaidAmount,
            AmountPaid = amountPaid,
            PaymentTimeUtc = paymentTimeUtc,
            ExitWindowExpiresUtc = exitWindowExpiresUtc,
            Status = "Succeeded"
        };
    }
}
