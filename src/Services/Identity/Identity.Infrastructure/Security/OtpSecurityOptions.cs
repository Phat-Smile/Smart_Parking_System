namespace ParkingSystem.Identity.Infrastructure.Security;

/// <summary>
/// Configuration options for OTP cryptographic security.
/// </summary>
public sealed class OtpSecurityOptions
{
    public const string SectionName = "Security:Otp";

    public const string MissingPepperMessage =
        "Security:Otp:Pepper is not configured. Set SECURITY_OTP_PEPPER in the repository .env file "
        + "(or run scripts/dev/Initialize-DevEnv.ps1), then start Identity again. See docs/development/Local-Development.md.";

    /// <summary>
    /// Server-managed cryptographic pepper used for HMAC-SHA256 OTP hashing.
    /// Must not be empty. Fail-fast on startup if unconfigured.
    /// </summary>
    public string Pepper { get; set; } = string.Empty;
}
