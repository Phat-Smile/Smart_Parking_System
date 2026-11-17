using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

// Danh mục mã lỗi báo cáo doanh thu
public static class OwnerRevenueErrors
{
    public static readonly Error ForbiddenRole = new(
        "payment.forbidden_role",
        "Only parking lot owners or administrators can view revenue reports.");

    public static readonly Error ForbiddenLotAccess = new(
        "payment.forbidden_lot_access",
        "You do not have permission to view revenue for this parking lot.");

    public static readonly Error ParkingLotIdRequired = new(
        "payment.parking_lot_id_required",
        "Parking lot id is required for lot owners.");

    public static readonly Error InvalidParkingLotId = new(
        "payment.invalid_parking_lot_id",
        "Parking lot id is invalid.");

    public static readonly Error InvalidTimeZoneOffset = new(
        "payment.invalid_timezone_offset",
        "Timezone offset must be between -720 and 840 minutes.");

    public static readonly Error ParkingServiceUnavailable = new(
        "payment.parking_service_unavailable",
        "Parking service is temporarily unavailable. Please try again.");
}
