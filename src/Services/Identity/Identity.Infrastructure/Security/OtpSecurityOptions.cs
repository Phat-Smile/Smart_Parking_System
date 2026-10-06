namespace ParkingSystem.Identity.Infrastructure.Security;

/// <summary>
/// Configuration options for OTP cryptographic security.
/// </summary>
public sealed class OtpSecurityOptions
{
    public const string SectionName = "Security:Otp";

    /// <summary>
    /// Server-managed cryptographic pepper used for HMAC-SHA256 OTP hashing.
    /// Must not be empty. Fail-fast on startup if unconfigured.
    /// </summary>
    public string Pepper { get; set; } = string.Empty;
}
