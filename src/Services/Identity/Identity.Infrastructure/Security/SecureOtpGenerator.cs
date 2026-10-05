using System.Security.Cryptography;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class SecureOtpGenerator : IOtpGenerator
{
    public string GenerateNumericOtp(int length = 6)
    {
        if (length <= 0 || length > 10)
            throw new ArgumentOutOfRangeException(nameof(length), "OTP length must be between 1 and 10.");

        var maxExclusive = (int)Math.Pow(10, length);
        var number = RandomNumberGenerator.GetInt32(0, maxExclusive);
        return number.ToString($"D{length}");
    }
}
