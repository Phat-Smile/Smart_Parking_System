using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Payment.Domain.Entities;

// Hóa đơn thanh toán tiền gửi xe
public class ParkingFeePayment
{
    public Guid Id { get; private set; }                            // Mã hóa đơn thanh toán
    public Guid SessionId { get; private set; }                     // Mã phiên đỗ xe
    public Guid ParkingLotId { get; private set; }                  // Mã bãi giữ xe
    public Guid? WalletId { get; private set; }                     // Mã ví nếu trả qua ví
    public Guid? AccountId { get; private set; }                    // Mã tài khoản người thanh toán
    public string PlateNumber { get; private set; } = null!;        // Biển số xe gửi
    public string VehicleType { get; private set; } = null!;        // Loại xe gửi
    public string PaymentMethod { get; private set; } = null!;      // Hình thức: Wallet, VNPay, Cash
    public long TotalFee { get; private set; }                      // Tổng tiền đỗ xe
    public long PrepaidAmount { get; private set; }                 // Tiền cọc đặt chỗ được trừ
    public long AmountPaid { get; private set; }                    // Số tiền thực trả
    public DateTimeOffset PaymentTimeUtc { get; private set; }      // Giờ chốt tính tiền
    public DateTimeOffset? ExitWindowExpiresUtc { get; private set; }// Giờ hết hạn lái xe ra
    public string Status { get; private set; } = null!;             // Trạng thái hóa đơn
    public int PaymentSequence { get; private set; } = 1;           // Thứ tự đợt trả trong phiên (1 = gốc)
    
    // Các trường đặc thù của cổng VNPay
    public string? TxnRef { get; private set; }                     // Mã tham chiếu gửi sang VNPay
    public string? PaymentUrl { get; private set; }                 // Đường link mã QR VNPay
    public DateTimeOffset? ExpiresAtUtc { get; private set; }       // Giờ hết hạn của mã QR
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }     // Giờ nhận tin báo từ VNPay
    public string? GatewayTransactionNo { get; private set; }       // Mã giao dịch phía VNPay
    public string? FailureCode { get; private set; }                // Mã lỗi nếu VNPay từ chối
    public string? SurplusRefundStatus { get; private set; }        // Tình trạng hoàn tiền thu thừa

    private ParkingFeePayment() { }

    // Tạo hóa đơn trừ tiền qua ví
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
        DateTimeOffset exitWindowExpiresUtc,
        int paymentSequence = 1)
    {
        return CreateWalletPayment(
            sessionId,
            parkingLotId,
            walletId,
            accountId,
            plateNumber,
            vehicleType,
            totalFee,
            prepaidAmount,
            amountPaid,
            paymentTimeUtc,
            exitWindowExpiresUtc,
            paymentSequence);
    }

    // Tạo hóa đơn trừ tiền qua ví
    public static ParkingFeePayment CreateWalletPayment(
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
        DateTimeOffset exitWindowExpiresUtc,
        int paymentSequence = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paymentSequence, 1);

        return new ParkingFeePayment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParkingLotId = parkingLotId,
            WalletId = walletId,
            AccountId = accountId,
            PlateNumber = plateNumber,
            VehicleType = vehicleType,
            PaymentMethod = Constants.PaymentMethod.Wallet,
            TotalFee = totalFee,
            PrepaidAmount = prepaidAmount,
            AmountPaid = amountPaid,
            PaymentTimeUtc = paymentTimeUtc,
            ExitWindowExpiresUtc = exitWindowExpiresUtc,
            Status = Constants.PaymentStatus.Succeeded,
            ConfirmedAtUtc = paymentTimeUtc,
            PaymentSequence = paymentSequence
        };
    }

    // Tao hoa don thu tien mat tai bai
    public static ParkingFeePayment CreateCashPayment(
        Guid sessionId,
        Guid parkingLotId,
        Guid? accountId,
        string plateNumber,
        string vehicleType,
        long totalFee,
        long prepaidAmount,
        long amountPaid,
        DateTimeOffset paymentTimeUtc,
        DateTimeOffset exitWindowExpiresUtc,
        int paymentSequence = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paymentSequence, 1);

        return new ParkingFeePayment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParkingLotId = parkingLotId,
            AccountId = accountId,
            PlateNumber = plateNumber,
            VehicleType = vehicleType,
            PaymentMethod = Constants.PaymentMethod.Cash,
            TotalFee = totalFee,
            PrepaidAmount = prepaidAmount,
            AmountPaid = amountPaid,
            PaymentTimeUtc = paymentTimeUtc,
            ExitWindowExpiresUtc = exitWindowExpiresUtc,
            Status = Constants.PaymentStatus.Succeeded,
            ConfirmedAtUtc = paymentTimeUtc,
            PaymentSequence = paymentSequence
        };
    }

    // Tạo hóa đơn chờ quét mã VNPay
    public static ParkingFeePayment CreateVnPayPayment(
        Guid sessionId,
        Guid parkingLotId,
        Guid? accountId,
        string plateNumber,
        string vehicleType,
        long totalFee,
        long prepaidAmount,
        long amountPaid,
        DateTimeOffset feeCalculatedAtUtc,
        DateTimeOffset expiresAtUtc,
        string txnRef,
        string paymentUrl,
        int paymentSequence = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paymentSequence, 1);

        return new ParkingFeePayment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParkingLotId = parkingLotId,
            AccountId = accountId,
            PlateNumber = plateNumber,
            VehicleType = vehicleType,
            PaymentMethod = Constants.PaymentMethod.VnPay,
            TotalFee = totalFee,
            PrepaidAmount = prepaidAmount,
            AmountPaid = amountPaid,
            PaymentTimeUtc = feeCalculatedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            TxnRef = txnRef,
            PaymentUrl = paymentUrl,
            Status = Constants.PaymentStatus.Pending,
            PaymentSequence = paymentSequence
        };
    }

    // Đổi sang trạng thái bị thay thế khi đổi mã mới
    public void MarkAsSuperseded()
    {
        if (Status != Constants.PaymentStatus.Pending) return;
        Status = Constants.PaymentStatus.Superseded;
    }

    // Xác nhận thanh toán thành công khi nhận IPN
    public void MarkAsSucceeded(string gatewayTxnNo, DateTimeOffset confirmedAtUtc, DateTimeOffset exitWindowExpiresUtc)
    {
        if (Status != Constants.PaymentStatus.Pending && Status != Constants.PaymentStatus.Superseded)
        {
            throw new InvalidOperationException($"Cannot mark payment in status '{Status}' as Succeeded.");
        }

        Status = Constants.PaymentStatus.Succeeded;
        GatewayTransactionNo = gatewayTxnNo;
        ConfirmedAtUtc = confirmedAtUtc;
        ExitWindowExpiresUtc = exitWindowExpiresUtc;
    }

    // Ghi nhận thanh toán thất bại từ VNPay
    public void MarkAsFailed(string failureCode, DateTimeOffset confirmedAtUtc)
    {
        Status = Constants.PaymentStatus.Failed;
        FailureCode = failureCode;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    // Ghi nhận mã QR hết hạn quét
    public void MarkAsExpired()
    {
        if (Status == Constants.PaymentStatus.Pending)
        {
            Status = Constants.PaymentStatus.Expired;
        }
    }

    // Ghi nhận tiền thu thừa do xe đã trả trước đó
    public void MarkAsSurplus(string gatewayTxnNo, string surplusRefundStatus, DateTimeOffset confirmedAtUtc)
    {
        Status = Constants.PaymentStatus.Surplus;
        GatewayTransactionNo = gatewayTxnNo;
        SurplusRefundStatus = surplusRefundStatus;
        ConfirmedAtUtc = confirmedAtUtc;
    }
}
