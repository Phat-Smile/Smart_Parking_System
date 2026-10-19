using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

public static class AuditLogErrors
{
    public static readonly Error InvalidPage =
        new("payment.invalid_page", "Page must be a whole number of 1 or more.");

    public static readonly Error InvalidPageSize =
        new("payment.invalid_page_size", "Page size must be between 1 and 50.");

    public static readonly Error InvalidActionType =
        new("payment.invalid_audit_action_type", "Action type is invalid.");

    public static readonly Error InvalidDateRange =
        new("payment.invalid_date_range", "Date range is invalid.");

    public static readonly Error AdminOnly =
        new("payment.admin_only", "Only system administrators can view audit logs.");

    public static readonly Error ReasonRequired =
        new("payment.audit_reason_required", "Reason is mandatory for audit log.");

    public static readonly Error DetailsKeyNotAllowed =
        new("payment.audit_details_key_not_allowed", "Audit details contains forbidden key.");
}
