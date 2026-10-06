using System.Security.Cryptography;
using System.Text;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenByteLength = 32;

    public (string RawToken, string TokenHash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var rawToken = Convert.ToHexString(bytes).ToLowerInvariant();

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return (rawToken, tokenHash);
    }
}
