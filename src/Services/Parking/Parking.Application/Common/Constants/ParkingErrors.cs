using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Common.Constants;

/// <summary>
/// Chứa toàn bộ các thông báo lỗi (Error Catalog) của Parking Service.
/// </summary>
public static class ParkingErrors
{
    public static class Pricing
    {
        public static readonly Error VersionConflict = new(
            "parking.pricing_version_conflict",
            "Bảng giá đã bị người khác thay đổi. Vui lòng tải lại trang.");

        public static readonly Error NotLotOwner = new(
            "parking.not_lot_owner",
            "Bạn không có quyền cấu hình bảng giá của bãi đỗ xe này.");

        public static readonly Error LotNotFound = new(
            "parking.lot_not_found",
            "Không tìm thấy bãi đỗ xe này trong hệ thống.");
    }
}
