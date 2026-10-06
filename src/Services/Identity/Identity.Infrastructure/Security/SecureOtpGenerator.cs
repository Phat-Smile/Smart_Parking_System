using System.Security.Cryptography;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class SecureOtpGenerator : IOtpGenerator
{
    public const int DefaultOtpLength = 6;

    public string GenerateNumericOtp(int length = DefaultOtpLength)
    {
        if (length != DefaultOtpLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Email verification only supports {DefaultOtpLength}-digit OTP codes.");
        }

        // Generates cryptographically secure integer between 0 and 999,999 inclusive.
        // D6 format guarantees leading zero preservation (e.g. 000042).
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6");
    }
}
