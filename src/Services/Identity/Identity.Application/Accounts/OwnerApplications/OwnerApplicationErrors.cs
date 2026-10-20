using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public static class OwnerApplicationErrors
{
    public static readonly Error ApplicationNotFound =
        new("identity.owner_application_not_found", "Không tìm thấy hồ sơ đăng ký chủ bãi.");

    public static readonly Error NotPending =
        new("identity.owner_application_not_pending", "Hồ sơ đăng ký chủ bãi không ở trạng thái chờ duyệt.");

    public static readonly Error NotRejected =
        new("identity.owner_application_not_rejected", "Chỉ hồ sơ bị từ chối mới có thể gửi lại yêu cầu duyệt.");

    public static readonly Error CannotApproveOwnApplication =
        new("identity.cannot_approve_own_application", "Quản trị viên không thể tự phê duyệt hồ sơ chủ bãi của chính mình.");

    public static readonly Error InvalidReason =
        new("identity.owner_reject_reason_required", "Lý do từ chối không được để trống và không vượt quá 500 ký tự.");

    public static readonly Error InvalidPageSize =
        new("identity.invalid_page_size", "Kích thước trang phải từ 1 đến 100.");
}

public sealed record OwnerApplicationActionResult(
    Guid AccountId,
    string Status,
    string Message);
