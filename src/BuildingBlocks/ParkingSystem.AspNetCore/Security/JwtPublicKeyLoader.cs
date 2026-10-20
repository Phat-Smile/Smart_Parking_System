using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ParkingSystem.AspNetCore.Security;

internal static class JwtPublicKeyLoader
{
    // searchParentFolders is true only in Development (see the callers): a relative path is then also looked up
    // in the parent folders, because `dotnet run` starts in the project folder while .local/ sits at the repository root.
    public static RSA LoadRsa(string path, bool searchParentFolders = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Security:Jwt:PublicKeyPath must be configured.");

        var fullPath = ResolvePath(path, searchParentFolders);
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

    private static string ResolvePath(string path, bool searchParentFolders)
    {
        var direct = Path.GetFullPath(path);
        if (!searchParentFolders || Path.IsPathRooted(path) || File.Exists(direct))
            return direct;

        var relative = path.Replace('\\', '/');
        var localIndex = relative.IndexOf(".local/", StringComparison.OrdinalIgnoreCase);
        string[] candidates = localIndex > 0 ? [relative, relative[localIndex..]] : [relative];
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
            {
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(folder.FullName, candidate.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
        }

        return direct;
    }
}

internal sealed class JwtSecurityOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtSecurityOptions>
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
            using var rsa = JwtPublicKeyLoader.LoadRsa(options.PublicKeyPath, environment.IsDevelopment());
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

    public JwtValidationKey(IOptions<JwtSecurityOptions> options, IHostEnvironment environment)
    {
        rsa = JwtPublicKeyLoader.LoadRsa(options.Value.PublicKeyPath, environment.IsDevelopment());
        SecurityKey = new RsaSecurityKey(rsa);
    }

    public SecurityKey SecurityKey { get; }

    public void Dispose() => rsa.Dispose();
}
