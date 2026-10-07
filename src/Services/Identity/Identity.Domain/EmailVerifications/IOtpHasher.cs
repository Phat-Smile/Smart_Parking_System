namespace ParkingSystem.Identity.Domain.EmailVerifications;

/// <summary>
/// Provides secure cryptographic hashing and verification for one-time passwords (OTP).
/// Uses HMAC-SHA256 with a server-managed secret pepper to mitigate offline brute-force attacks.
/// </summary>
public interface IOtpHasher
{
    /// <summary>
    /// Computes a keyed HMAC-SHA256 hash for the specified numeric OTP and account ID.
    /// </summary>
    string HashOtp(string otpCode, Guid accountId);

    /// <summary>
    /// Verifies the candidate OTP against the stored expected hash using constant-time comparison.
    /// </summary>
    bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash);
}
