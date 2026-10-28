using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class JwtTokenIssuerTests : IDisposable
{
    private readonly string _tempDir;

    public JwtTokenIssuerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jwt-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Constructor_throws_InvalidOperationException_when_PrivateKeyPath_is_empty()
    {
        var opts = Options.Create(new JwtIssuerOptions { PrivateKeyPath = "" });
        Assert.Throws<InvalidOperationException>(() => new JwtTokenIssuer(opts));
    }

    [Fact]
    public void Constructor_throws_InvalidOperationException_when_PrivateKeyPath_is_whitespace()
    {
        var opts = Options.Create(new JwtIssuerOptions { PrivateKeyPath = "   " });
        Assert.Throws<InvalidOperationException>(() => new JwtTokenIssuer(opts));
    }

    [Fact]
    public void Constructor_throws_FileNotFoundException_when_key_file_does_not_exist()
    {
        var opts = Options.Create(new JwtIssuerOptions
        {
            PrivateKeyPath = Path.Combine(_tempDir, "nonexistent.pem")
        });

        var ex = Assert.Throws<FileNotFoundException>(() => new JwtTokenIssuer(opts));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_throws_when_PEM_content_is_corrupt()
    {
        var pemPath = Path.Combine(_tempDir, "corrupt.pem");
        File.WriteAllText(pemPath, "-----BEGIN RSA PRIVATE KEY-----\nthisis not valid base64!@#\n-----END RSA PRIVATE KEY-----");

        var opts = Options.Create(new JwtIssuerOptions { PrivateKeyPath = pemPath });

        var ex = Assert.Throws<InvalidOperationException>(() => new JwtTokenIssuer(opts));
        Assert.Contains("Failed to import", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_throws_when_RSA_key_is_smaller_than_2048_bits()
    {
        var pemPath = Path.Combine(_tempDir, "small.pem");
        using var rsa = RSA.Create(1024);
        File.WriteAllText(pemPath, rsa.ExportRSAPrivateKeyPem());

        var opts = Options.Create(new JwtIssuerOptions { PrivateKeyPath = pemPath });

        var ex = Assert.Throws<InvalidOperationException>(() => new JwtTokenIssuer(opts));
        Assert.Contains("2048", ex.Message);
    }

    [Fact]
    public void Constructor_succeeds_and_IssueAccessToken_returns_three_part_JWT_with_valid_2048_bit_key()
    {
        var pemPath = Path.Combine(_tempDir, "valid.pem");
        using var rsa = RSA.Create(2048);
        File.WriteAllText(pemPath, rsa.ExportRSAPrivateKeyPem());

        var opts = Options.Create(new JwtIssuerOptions
        {
            PrivateKeyPath = pemPath,
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 15
        });

        using var issuer = new JwtTokenIssuer(opts);

        var account = ParkingSystem.Identity.Domain.Accounts.Account.Register(
            Guid.NewGuid(), "user@test.com", "hash", null, DateTimeOffset.UtcNow);

        var token = issuer.IssueAccessToken(account, DateTimeOffset.UtcNow);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }
}
