namespace ParkingSystem.Payment.Domain.Constants;

public static class DomainErrorMessages
{
    // PHẦN 1: CÁC THÔNG BÁO LỖI CỦA (WALLET)
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

    // PHẦN 2: CÁC THÔNG BÁO LỖI CỦA TÍNH PHÍ (PRICING)
    public static class Pricing
    {
        public const string BlockMinutesMustBePositive = "Thời gian của một block bắt buộc phải lớn hơn 0.";
        public const string EndTimeBeforeStartTime = "Giờ ra không hợp lệ vì nhỏ hơn giờ vào.";
    }
}
