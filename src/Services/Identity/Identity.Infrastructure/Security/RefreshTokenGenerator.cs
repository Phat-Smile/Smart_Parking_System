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
        var tokenHash = HashToken(rawToken);

        return (rawToken, tokenHash);
    }

    public string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
