using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class JwtTokenIssuer : IJwtTokenIssuer, IDisposable
{
    private readonly RSA rsa;
    private readonly string issuer;
    private readonly string audience;
    private readonly int expirationSeconds;

    public int AccessTokenExpirationSeconds => expirationSeconds;

    public JwtTokenIssuer(IOptions<JwtIssuerOptions> options)
    {
        var settings = options.Value;
        issuer = settings.Issuer;
        audience = settings.Audience;
        expirationSeconds = settings.ExpirationMinutes * 60;

        rsa = LoadPrivateKey(settings.PrivateKeyPath);
    }

    public string IssueAccessToken(Account account, Guid sessionId, DateTimeOffset nowUtc)
    {
        var header = new { alg = "RS256", typ = "JWT" };
        var headerJson = JsonSerializer.Serialize(header);
        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        var exp = nowUtc.AddSeconds(expirationSeconds).ToUnixTimeSeconds();
        var iat = nowUtc.ToUnixTimeSeconds();
        var nbf = iat;

        var payload = new Dictionary<string, object>
        {
            ["sub"] = account.Id.ToString(),
            ["email"] = account.Email,
            ["role"] = account.Role.ToString(),
            ["sid"] = sessionId.ToString(),
            ["iss"] = issuer,
            ["aud"] = audience,
            ["jti"] = Guid.NewGuid().ToString(),
            ["iat"] = iat,
            ["nbf"] = nbf,
            ["exp"] = exp
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var dataToSign = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");
        var signatureBytes = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Base64UrlEncode(signatureBytes);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    private static RSA LoadPrivateKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Security:Jwt:PrivateKeyPath must be configured.");

        var resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"The configured JWT private key file was not found: {path}");

        var pem = File.ReadAllText(resolvedPath).Trim();
        var rsaInstance = RSA.Create();
        try
        {
            rsaInstance.ImportFromPem(pem);
            if (rsaInstance.KeySize < 2048)
                throw new InvalidOperationException("The configured JWT private key must be at least 2048 bits.");

            return rsaInstance;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            rsaInstance.Dispose();
            throw new InvalidOperationException($"Failed to import RSA private key from {resolvedPath}: {ex.Message}", ex);
        }
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, path);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return Path.GetFullPath(path);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public void Dispose() => rsa.Dispose();
}
