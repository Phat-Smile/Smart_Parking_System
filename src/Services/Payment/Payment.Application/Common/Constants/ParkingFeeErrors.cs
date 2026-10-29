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
