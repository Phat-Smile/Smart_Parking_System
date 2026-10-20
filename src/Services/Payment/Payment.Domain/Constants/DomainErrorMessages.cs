namespace ParkingSystem.Payment.Domain.Constants;

public static class DomainErrorMessages
{
    // PHẦN 1: CÁC THÔNG BÁO LỖI CỦA VÍ (WALLET)
    public static class Wallet
    {
        public const string NotEnoughMoney = "Wallet balance is insufficient to perform the transaction.";
        public const string AlreadyClosed = "Cannot perform transactions on a closed wallet.";
        public const string MustBeZeroToClose = "Cannot close wallet. The wallet balance must be zero.";
        public const string InvalidDepositAmount = "Deposit amount must be greater than zero.";
        public const string InvalidFeeAmount = "Fee payment amount must be negative.";
        public const string AmountCannotBeZero = "Transaction amount is invalid. Please enter an amount other than zero.";
        public const string AmountMustBePositive = "Top-up or credit transactions must be greater than zero.";
        public const string AmountMustBeNegative = "Payment or debit transactions must be less than zero.";
        public const string AmountMustBeInteger = "Transaction amount in VND must be an integer without decimal fractions.";
    }

    // PHẦN 2: CÁC THÔNG BÁO LỖI CỦA TÍNH PHÍ (PRICING)
    public static class Pricing
    {
        public const string BlockMinutesMustBePositive = "Block duration in minutes must be greater than zero.";
        public const string EndTimeBeforeStartTime = "Exit time is invalid because it is earlier than entry time.";
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
