using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

// Tokens are signed with a local RSA key standing in for Google's published key; nothing calls Google.
public sealed class GoogleIdTokenValidatorTests
{
    private const string ClientId = "smart-parking-web.apps.googleusercontent.com";
    private static readonly RsaSecurityKey GoogleKey = new(RSA.Create(2048)) { KeyId = "google-test-key" };
    private static readonly RsaSecurityKey OtherKey = new(RSA.Create(2048)) { KeyId = "attacker-key" };

    [Fact]
    public async Task Accepts_a_google_signed_token_for_our_client_with_a_verified_email()
    {
        var identity = await Validator().ValidateAsync(Token(), TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal("google-sub-123", identity.Subject);
        Assert.Equal("an.nguyen@gmail.com", identity.Email);
        Assert.Equal("Nguyễn Văn An", identity.Name);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("unverified-email")]
    [InlineData("signature")]
    public async Task Rejects_tokens_that_are_not_valid_for_us(string defect)
    {
        var token = defect switch
        {
            "audience" => Token(audience: "someone-else.apps.googleusercontent.com"),
            "issuer" => Token(issuer: "https://evil.example.com"),
            "expired" => Token(expires: DateTime.UtcNow.AddMinutes(-10)),
            "unverified-email" => Token(emailVerified: "false"),
            _ => Token(key: OtherKey)
        };

        Assert.Null(await Validator().ValidateAsync(token, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_client_id_google_sign_in_is_off()
    {
        var validator = Validator(clientId: "");

        Assert.False(validator.IsConfigured);
        Assert.Null(await validator.ValidateAsync(Token(), TestContext.Current.CancellationToken));
    }

    private static GoogleIdTokenValidator Validator(string clientId = ClientId)
    {
        var configuration = new OpenIdConnectConfiguration();
        configuration.SigningKeys.Add(GoogleKey);
        return new GoogleIdTokenValidator(
            Options.Create(new GoogleAuthOptions { ClientId = clientId }),
            NullLogger<GoogleIdTokenValidator>.Instance,
            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));
    }

    private static string Token(
        string audience = ClientId,
        string issuer = "https://accounts.google.com",
        DateTime? expires = null,
        string emailVerified = "true",
        SecurityKey? key = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-60),
            NotBefore = expiresAt.AddMinutes(-60),
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-sub-123"),
                new Claim("email", "an.nguyen@gmail.com"),
                new Claim("email_verified", emailVerified),
                new Claim("name", "Nguyễn Văn An")
            ]),
            SigningCredentials = new SigningCredentials(key ?? GoogleKey, SecurityAlgorithms.RsaSha256)
        });
    }
}
