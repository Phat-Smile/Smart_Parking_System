using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Common.Constants;

public static class ParkingErrors
{
    public static class Pricing
    {
        // Các mã lỗi hiện có của bạn
        public static readonly Error VersionConflict = new(
            "parking.pricing_version_conflict",
            "Bảng giá đã bị người khác thay đổi. Vui lòng tải lại trang.");

        public static readonly Error NotLotOwner = new(
            "parking.not_lot_owner",
            "Bạn không có quyền cấu hình bảng giá của bãi đỗ xe này.");

        public static readonly Error LotNotFound = new(
            "parking.lot_not_found",
            "Không tìm thấy bãi đỗ xe này trong hệ thống.");

        public static readonly Error LotIdEmpty = new(
            "parking.pricing_validation_lot_empty",
            "LotId không được để trống.");

        public static readonly Error VehicleTypeEmpty = new(
            "parking.pricing_validation_vehicle_empty",
            "Loại xe không được để trống.");

        public static readonly Error PriceNegative = new(
            "parking.pricing_validation_price_negative",
            "Giá tiền phải là số nguyên lớn hơn 0.");

        public static readonly Error OvernightFeeNegative = new(
            "parking.pricing_validation_overnight_fee_negative",
            "Phí qua đêm không được là số âm.");

        public static readonly Error BlockMinutesInvalid = new(
            "parking.pricing_validation_block_minutes_invalid",
            "Thời gian block phải là 15, 30 hoặc 60 phút khi tính theo Block, và phải để trống khi tính theo PerEntry.");

        // Bổ sung thêm các mã lỗi chuẩn theo API Design
        public static readonly Error VehicleTypeInvalid = new(
            "parking.pricing_validation_vehicle_type_invalid",
            "Loại xe phải là CAR hoặc MOTORBIKE.");

        public static readonly Error BillingModeInvalid = new(
            "parking.pricing_validation_billing_mode_invalid",
            "Chế độ tính phí phải là PerEntry hoặc Block.");

        public static readonly Error PriceMustBeInteger = new(
            "parking.pricing_validation_price_not_integer",
            "Giá tiền phải là số nguyên VND, không được có phần lẻ thập phân.");

        public static readonly Error OvernightFeeMustBeInteger = new(
            "parking.pricing_validation_overnight_fee_not_integer",
            "Phí qua đêm phải là số nguyên VND, không được có phần lẻ thập phân.");

        public static readonly Error ReasonRequired = new(
            "parking.pricing_validation_reason_required",
            "Lý do thay đổi không được để trống khi chỉnh sửa bảng giá.");

        public static readonly Error ReasonTooLong = new(
            "parking.pricing_validation_reason_too_long",
            "Lý do thay đổi không được vượt quá 500 ký tự.");

        public static readonly Error PolicyNotFound = new(
            "parking.pricing_not_found",
            "Chưa có bảng giá cho loại xe này tại bãi đỗ xe.");
    }

    public static class DepositPolicy
    {
        public static readonly Error LotNotFound = new(
            "parking.deposit_policy.lot_not_found",
            "Không tìm thấy bãi đỗ xe này trong hệ thống.");

        public static readonly Error NotLotOwner = new(
            "parking.deposit_policy.not_lot_owner",
            "Bạn không có quyền cấu hình mức cọc của bãi đỗ xe này.");

        public static readonly Error LotIdEmpty = new(
            "parking.deposit_policy.lot_id_empty",
            "LotId không được để trống.");

        public static readonly Error DepositNegative = new(
            "parking.deposit_policy.deposit_negative",
            "Mức cọc không được là số âm.");

        public static readonly Error DepositMustBeInteger = new(
            "parking.deposit_policy.deposit_must_be_integer",
            "Mức cọc phải là số nguyên VND, không được có phần lẻ thập phân.");

        public static readonly Error DepositRequired = new(
            "parking.deposit_policy.deposit_required",
            "Mức cọc không được để trống.");
    }

    public static class ReservationRatioPolicy
    {
        public static readonly Error LotNotFound = new(
            "parking.reservation_ratio_policy.lot_not_found",
            "Không tìm thấy bãi đỗ xe này trong hệ thống.");

        public static readonly Error NotLotOwner = new(
            "parking.reservation_ratio_policy.not_lot_owner",
            "Bạn không có quyền cấu hình tỷ lệ suất đặt trước của bãi đỗ xe này.");

        public static readonly Error LotIdEmpty = new(
            "parking.reservation_ratio_policy.lot_id_empty",
            "LotId không được để trống.");

        public static readonly Error RatioRequired = new(
            "parking.reservation_ratio_policy.ratio_required",
            "Tỷ lệ suất đặt trước không được để trống.");

        public static readonly Error RatioOutOfRange = new(
            "parking.reservation_ratio_policy.ratio_out_of_range",
            "Tỷ lệ suất đặt trước phải là số nguyên từ 0 đến 100.");
    }
}
