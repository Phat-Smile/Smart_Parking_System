using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ParkingSystem.AspNetCore.Security;

internal static class JwtPublicKeyLoader
{
    public static RSA LoadRsa(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Security:Jwt:PublicKeyPath must be configured.");

        var fullPath = ResolvePath(path);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            throw new InvalidOperationException(
                $"The configured JWT public key file does not exist at '{path}'. " +
                "If running locally, please run 'scripts/security/New-DevelopmentJwtKey.ps1' or 'dotnet run --project scripts/security/New-DevelopmentJwtKey.cs' to generate development keys.");

        var pem = File.ReadAllText(fullPath).Trim();
        if (!pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) ||
            !pem.EndsWith("-----END PUBLIC KEY-----", StringComparison.Ordinal) ||
            Count(pem, "-----BEGIN") != 1 ||
            Count(pem, "-----END") != 1)
        {
            throw new InvalidOperationException("The configured JWT key must contain one PUBLIC KEY PEM block only.");
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
                throw new InvalidOperationException("The configured JWT public key must be at least 2048 bits.");

            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw new InvalidOperationException("The configured JWT public key is invalid.");
        }
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }
        return count;
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        var direct = Path.GetFullPath(path);
        if (File.Exists(direct))
            return direct;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, path);
            if (File.Exists(candidate))
                return candidate;

            var normalized = path.Replace('\\', '/');
            var localIdx = normalized.IndexOf(".local/", StringComparison.OrdinalIgnoreCase);
            if (localIdx >= 0)
            {
                var relFromLocal = normalized[localIdx..].Replace('/', Path.DirectorySeparatorChar);
                var candidateLocal = Path.Combine(current.FullName, relFromLocal);
                if (File.Exists(candidateLocal))
                    return candidateLocal;
            }

            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, path);
            if (File.Exists(candidate))
                return candidate;

            var normalized = path.Replace('\\', '/');
            var localIdx = normalized.IndexOf(".local/", StringComparison.OrdinalIgnoreCase);
            if (localIdx >= 0)
            {
                var relFromLocal = normalized[localIdx..].Replace('/', Path.DirectorySeparatorChar);
                var candidateLocal = Path.Combine(current.FullName, relFromLocal);
                if (File.Exists(candidateLocal))
                    return candidateLocal;
            }

            current = current.Parent;
        }

        return direct;
    }
}

internal sealed class JwtSecurityOptionsValidator : IValidateOptions<JwtSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtSecurityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
            return ValidateOptionsResult.Fail("Security:Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(options.Audience))
            return ValidateOptionsResult.Fail("Security:Jwt:Audience is required.");
        if (options.ClockSkewSeconds is < 0 or > 300)
            return ValidateOptionsResult.Fail("Security:Jwt:ClockSkewSeconds must be between 0 and 300.");
        if (string.IsNullOrWhiteSpace(options.PublicKeyPath))
            return ValidateOptionsResult.Fail("Security:Jwt:PublicKeyPath is required.");

        try
        {
            using var rsa = JwtPublicKeyLoader.LoadRsa(options.PublicKeyPath);
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException)
        {
            return ValidateOptionsResult.Fail($"JWT public-key configuration is invalid: {exception.Message}");
        }
    }
}

internal sealed class JwtValidationKey : IDisposable
{
    private readonly RSA rsa;

    public JwtValidationKey(IOptions<JwtSecurityOptions> options)
    {
        rsa = JwtPublicKeyLoader.LoadRsa(options.Value.PublicKeyPath);
        SecurityKey = new RsaSecurityKey(rsa);
    }

    public SecurityKey SecurityKey { get; }

    public void Dispose() => rsa.Dispose();
}
