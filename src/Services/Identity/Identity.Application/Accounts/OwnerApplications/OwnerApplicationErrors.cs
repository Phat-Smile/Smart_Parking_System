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

    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Không tìm thấy tài khoản.");

    public static readonly Error AlreadyApproved =
        new("identity.owner_application_already_approved", "Tài khoản này đã là chủ bãi đỗ xe.");

    public static readonly Error AlreadyPending =
        new("identity.owner_application_already_pending", "Tài khoản đã có hồ sơ đăng ký chủ bãi đang chờ duyệt.");

    public static readonly Error InvalidPageSize =
        new("identity.invalid_page_size", "Kích thước trang phải từ 1 đến 100.");

    public static readonly Error OwnerTypeRequired =
        new("identity.owner_type_required", "Vui lòng chọn loại chủ thể (Cá nhân, Hộ kinh doanh hoặc Doanh nghiệp).");

    public static readonly Error OwnerTypeInvalid =
        new("identity.owner_type_invalid", "Loại chủ thể phải là Individual, HouseholdBusiness hoặc Enterprise.");

    public static readonly Error ApplicationLocked =
        new("identity.owner_application_locked", "Hồ sơ chủ bãi đã được duyệt và không thể chỉnh sửa.");

    public static readonly Error DocumentInvalidType =
        new("identity.owner_document_invalid_type", "Loại giấy tờ không thuộc danh mục quy định.");

    public static readonly Error DocumentUnsupportedContent =
        new("identity.owner_document_unsupported_content", "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận tệp PDF, PNG hoặc JPEG.");

    public static readonly Error DocumentTooLarge =
        new("identity.owner_document_too_large", "Dung lượng tệp vượt quá giới hạn tối đa cho phép.");

    public static readonly Error DocumentLimitReached =
        new("identity.owner_document_limit_reached", "Đã đạt số lượng giấy tờ tối đa cho phép.");

    public static readonly Error DocumentNotFound =
        new("identity.owner_document_not_found", "Không tìm thấy giấy tờ yêu cầu.");

    public static readonly Error AccountNotVerified =
        new("identity.account_not_verified", "Tài khoản cần xác thực email trước khi đăng ký xét duyệt chủ bãi.");

    public static readonly Error CannotRejectOwnApplication =
        new("identity.cannot_reject_own_application", "Quản trị viên không thể tự từ chối hồ sơ chủ bãi của chính mình.");

    public static readonly Error TaxCodeAlreadyRegistered =
        new("identity.owner_tax_code_already_registered", "Mã số thuế này đã được đăng ký cho một tài khoản chủ bãi khác.");

    public static readonly Error DocumentsIncomplete =
        new("identity.owner_documents_incomplete", "Hồ sơ chưa nộp đủ các giấy tờ bắt buộc theo quy định.");
}

public sealed record OwnerApplicationActionResult(
    Guid AccountId,
    string Status,
    string Message);
