namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;

// Kết quả trả về khi thanh toán ví thành công
public sealed record PayParkingFeeByWalletResponse(
    Guid TransactionId,                 // Mã hóa đơn thanh toán
    Guid SessionId,                     // Mã phiên đỗ xe
    long AmountPaid,                    // Số tiền đã trừ ví
    long WalletBalanceRemaining,        // Số dư ví còn lại sau trừ
    DateTimeOffset PaymentTimeUtc,      // Giờ thanh toán chuẩn quốc tế
    DateTimeOffset ExitWindowExpiresUtc,// Giờ hết hạn ra khỏi bãi
    bool IsReplayed = false             // Đánh dấu kết quả cũ phát lại
);

// Chi tiết số tiền thiếu khi ví không đủ
public sealed record WalletInsufficientBalanceDetails(
    long CurrentBalance,                // Số dư ví hiện tại
    long AmountDue,                     // Số tiền cần phải thanh toán
    long ShortfallAmount                // Số tiền còn thiếu cần nạp thêm
);

// Chi tiết số tiền mới khi giá bị đổi
public sealed record FeeChangedDetails(
    long CurrentAmountDue               // Mức giá mới tính theo giờ thực
);
