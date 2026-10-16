namespace ParkingSystem.Payment.Domain.Constants;

public static class DomainErrorMessages
{
    // CÁC THÔNG BÁO LỖI LIÊN QUAN ĐẾN VÍ (WALLET)
    public static class Wallet
    {
        public const string NotEnoughMoney = "Số dư trong ví không đủ để thực hiện giao dịch.";
        public const string AlreadyClosed = "Không thể thực hiện giao dịch trên ví đã bị đóng.";
        public const string MustBeZeroToClose = "Không thể đóng ví. Số dư của ví bắt buộc phải bằng 0.";
        public const string InvalidDepositAmount = "Số tiền nạp bắt buộc phải lớn hơn 0.";
        public const string InvalidFeeAmount = "Số tiền thanh toán phí bắt buộc phải là số âm.";
        public const string AmountCannotBeZero = "Số tiền giao dịch không hợp lệ. Vui lòng nhập số tiền khác 0.";
        public const string AmountMustBePositive = "Giao dịch nạp hoặc nhận thêm tiền bắt buộc phải lớn hơn 0.";
        public const string AmountMustBeNegative = "Giao dịch thanh toán hoặc khấu trừ bắt buộc phải nhỏ hơn 0.";
        public const string AmountMustBeInteger = "Số tiền giao dịch VND phải là số nguyên, không được có phần lẻ thập phân.";
    }

    // CÁC THÔNG BÁO LỖI LIÊN QUAN ĐẾN TÍNH PHÍ (PRICING)
    public static class Pricing
    {
        public const string BlockMinutesMustBePositive = "Thời gian của một block bắt buộc phải lớn hơn 0.";
        public const string EndTimeBeforeStartTime = "Giờ ra không hợp lệ vì nhỏ hơn giờ vào.";
        public const string ExitTimeBeforePaymentTime = "Giờ ra không hợp lệ vì nhỏ hơn giờ thanh toán.";
        public const string GracePeriodMinutesNegative = "Thời gian ân hạn không được là số âm.";
    }

    // PHẦN 3: CÁC THÔNG BÁO LỖI CỦA GIAO DỊCH VNPAY (VNPAY TRANSACTION)
    public static class VnpayTransaction
    {
        public const string InvalidAmount = "VNPay transaction amount must be a positive integer.";
        public const string InvalidWalletId = "Associated wallet identifier is invalid.";
        public const string InvalidTxnRef = "VNPay transaction reference (TxnRef) is invalid (must be 32 hexadecimal characters).";
        public const string InvalidClientRequestKey = "Idempotency key is invalid (must be between 8 and 64 characters).";
        public const string InvalidExpiryTime = "Transaction expiration time must be later than creation time.";
        public const string EmptyTransactionId = "Transaction identifier cannot be empty.";
    }
}
