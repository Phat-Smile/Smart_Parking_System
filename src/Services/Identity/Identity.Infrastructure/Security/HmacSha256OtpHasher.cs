using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Infrastructure.Security;

/// <summary>
/// Implements HMAC-SHA256 OTP hashing using a server-side secret pepper.
/// Prevents offline rainbow-table and dictionary attacks even if the database is compromised.
/// </summary>
public sealed class HmacSha256OtpHasher : IOtpHasher
{
    private readonly byte[] _pepperBytes;

    public HmacSha256OtpHasher(IOptions<OtpSecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pepper = options.Value.Pepper;
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException(OtpSecurityOptions.MissingPepperMessage);
        }

        _pepperBytes = Encoding.UTF8.GetBytes(pepper.Trim());
    }

    public string HashOtp(string otpCode, Guid accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));

        var rawBytes = Encoding.UTF8.GetBytes($"{accountId:D}:{otpCode.Trim()}");
        var hashBytes = HMACSHA256.HashData(_pepperBytes, rawBytes);
        return Convert.ToHexString(hashBytes);
    }

    public bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(candidateOtp) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var computedHash = HashOtp(candidateOtp, accountId);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash.Trim());
        var actualBytes = Encoding.UTF8.GetBytes(computedHash);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
