namespace ParkingSystem.Payment.Domain.Constants;

/// <summary>
/// Danh mục hành động kiểm toán (Action Catalog) của toàn hệ thống.
/// </summary>
public static class AuditActionCatalog
{
    // Payment Service
    public const string CashPaymentConfirmed = "CashPaymentConfirmed";
    public const string WalletEntryReversed = "WalletEntryReversed";
    public const string RefundRequestCreated = "RefundRequestCreated";
    public const string RefundRequestApproved = "RefundRequestApproved";
    public const string RefundRequestRejected = "RefundRequestRejected";

    // Parking Service
    public const string PricingPolicyChanged = "PricingPolicyChanged";
    public const string SlotStatusOverridden = "SlotStatusOverridden";
    public const string ManualParkingConfirmed = "ManualParkingConfirmed";
    public const string ManualExitVerified = "ManualExitVerified";
    public const string SessionQrReissued = "SessionQrReissued";

    // Identity Service
    public const string LoginFailed = "LoginFailed";
    public const string AccountLocked = "AccountLocked";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string RefreshTokenReuseDetected = "RefreshTokenReuseDetected";

    private static readonly HashSet<string> AllActions = new(StringComparer.Ordinal)
    {
        CashPaymentConfirmed,
        WalletEntryReversed,
        RefundRequestCreated,
        RefundRequestApproved,
        RefundRequestRejected,
        PricingPolicyChanged,
        SlotStatusOverridden,
        ManualParkingConfirmed,
        ManualExitVerified,
        SessionQrReissued,
        LoginFailed,
        AccountLocked,
        PasswordChanged,
        PasswordReset,
        RefreshTokenReuseDetected
    };

    public static bool IsValid(string? actionType) =>
        !string.IsNullOrWhiteSpace(actionType) && AllActions.Contains(actionType);
}
