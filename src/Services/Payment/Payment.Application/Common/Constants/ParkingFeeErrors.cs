using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

// Danh mục mã lỗi khi tính phí đỗ xe
public static class ParkingFeeErrors
{
    // Lỗi mã phiên đỗ xe sai định dạng
    public static readonly Error SessionIdInvalid =
        new("payment.session_id_invalid", "Session id is invalid.");

    // Lỗi chưa đăng nhập tài khoản
    public static readonly Error Unauthenticated =
        new("payment.unauthenticated", "Authentication is required.");

    // Lỗi không có quyền xem phiên đỗ này
    public static readonly Error SessionAccessDenied =
        new("payment.session_access_denied",
            "You do not have permission to view the fee of this parking session.");

    // Lỗi không tìm thấy phiên đỗ xe
    public static readonly Error SessionNotFound =
        new("payment.session_not_found", "Parking session not found.");

    // Lỗi phiên đỗ không ở trạng thái đang đỗ
    public static readonly Error SessionNotActive =
        new("payment.session_not_active", "Parking session is not in Active status.");

    // Lỗi dịch vụ bãi xe tạm thời không phản hồi
    public static readonly Error ParkingServiceUnavailable =
        new("payment.parking_service_unavailable",
            "Parking service is temporarily unavailable. Please try again.");
}
// Danh mục lỗi thanh toán tiền gửi xe
public static class PayParkingFeeErrors
{
    public static readonly Error IdempotencyKeyInvalid = 
        new("payment.idempotency_key_invalid", 
            "X-Idempotency-Key header is required and must be a valid GUID.");
    public static readonly Error SessionIdInvalid = 
        new("payment.session_id_invalid", 
            "Session id is invalid.");
    public static readonly Error ExpectedAmountInvalid = 
        new("payment.expected_amount_invalid", 
            "Expected amount due must be an integer greater than 0.");
    public static readonly Error RoleNotAllowed = 
        new("payment.role_not_allowed", 
            "Only customers can pay a parking fee from a wallet.");
    public static readonly Error SessionAccessDenied = 
        new("payment.session_access_denied", 
            "You do not have permission to pay for this parking session.");
    public static readonly Error SessionNotFound = 
        new("payment.session_not_found", "Parking session not found.");
    public static readonly Error WalletNotFound = 
        new("payment.wallet_not_found", 
            "Wallet not found.");
    public static readonly Error SessionNotActive = 
        new("payment.session_not_active", 
            "Parking session is not in Active status.");
    public static readonly Error SessionAlreadyPaid = 
        new("payment.session_already_paid", 
            "Parking session has already been paid.");
    public static readonly Error NoAmountDue = 
        new("payment.no_amount_due", "There is no amount due for this parking session.");
    public static readonly Error FeeChanged = 
        new("payment.fee_changed", 
            "The parking fee has changed. Please review the new amount.");
    public static readonly Error IdempotencyKeyReused = 
        new("payment.idempotency_key_reused", 
            "Idempotency key was already used with a different request.");
    public static readonly Error RequestInProgress = 
        new("payment.request_in_progress", 
            "A request with this idempotency key is still being processed.");
    public static readonly Error ParkingServiceUnavailable = 
        new("payment.parking_service_unavailable", 
            "Parking service is temporarily unavailable. Please try again.");
    public static readonly Error WalletBusy = 
        new("payment.wallet_busy", 
            "Payment could not be completed. Please try again.");
    public static readonly Error WalletInsufficientBalance = 
        new("payment.wallet_insufficient_balance", 
            "Wallet balance is not enough to pay this parking fee.");
    public static readonly Error RoleNotAllowedVNPay = 
        new("payment.role_not_allowed", 
            "Your role cannot create a VNPay payment.");
    public static readonly Error VnPayAmountNotSupported = 
        new("payment.vnpay_amount_not_supported", 
            "This amount cannot be paid through VNPay.");
    public static readonly Error VnPayUrlGenerationFailed = 
        new("payment.vnpay_url_generation_failed", 
            "Failed to generate VNPay payment URL.");
    public static readonly Error RoleNotAllowedCash = 
        new("payment.role_not_allowed", 
            "Only staff or parking lot owner can confirm cash payment.");
    public static readonly Error LotAccessDenied = 
        new("payment.lot_access_denied", 
            "You do not have permission to confirm payment for this parking lot.");
    public static readonly Error PaymentAlreadyInProgress = 
        new("payment.payment_already_in_progress", 
            "Another payment is already in progress for this session.");
    public static readonly Error ReasonTooLong = 
        new("payment.reason_too_long", 
            "Reason cannot exceed 500 characters.");
}

// Danh mục lỗi khi tra cứu trạng thái hóa đơn
public static class ParkingFeePaymentErrors
{
    public static readonly Error TransactionIdInvalid =
        new("payment.transaction_id_invalid",
            "Transaction id is invalid.");
    public static readonly Error TransactionNotFound =
        new("payment.transaction_not_found",
            "Parking fee payment not found.");
}
