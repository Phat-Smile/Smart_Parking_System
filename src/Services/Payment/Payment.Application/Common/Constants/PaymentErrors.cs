using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

public static class PaymentErrors
{
    public static class Wallet
    {
        public static readonly Error InvalidPage =
            new("payment.invalid_page", "Page must be a whole number of 1 or more.");

        public static readonly Error InvalidPageSize =
            new("payment.invalid_page_size", "Page size must be between 1 and 50.");

        public static readonly Error InvalidEntryType =
            new("payment.invalid_entry_type", "Entry type is invalid.");

        public static readonly Error InvalidDateRange =
            new("payment.invalid_date_range", "Date range is invalid.");

        public static readonly Error CustomerOnly =
            new("payment.customer_only", "Only registered customers have a wallet.");
    }

    public static class Topup
    {
        // Loi ma giao dich nap tien khong hop le
        public static readonly Error InvalidId = new("payment.invalid_topup_id", "Top-up id is invalid.");

        // Loi khong tim thay giao dich hoac khong chinh chu
        public static readonly Error NotFound = new("payment.topup_not_found", "Top-up not found.");
    }
}
