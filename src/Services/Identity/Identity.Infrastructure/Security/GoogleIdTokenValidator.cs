using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    // OAuth client ID of the web app (Google Cloud console). Empty switches Google sign-in off.
    public string? ClientId { get; set; }
}

// G1: validates the ID token the frontend got from Google Identity Services against Google's published keys.
public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private const string Discovery = "https://accounts.google.com/.well-known/openid-configuration";
    private static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];

    private readonly string? _clientId;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly ILogger<GoogleIdTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new();

    public GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options, ILogger<GoogleIdTokenValidator> logger)
        : this(options, logger, new ConfigurationManager<OpenIdConnectConfiguration>(
            Discovery, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true }))
    {
    }

    public GoogleIdTokenValidator(
        IOptions<GoogleAuthOptions> options,
        ILogger<GoogleIdTokenValidator> logger,
        IConfigurationManager<OpenIdConnectConfiguration> configuration)
    {
        _clientId = options.Value.ClientId?.Trim();
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_clientId);

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            return null;

        var keys = await _configuration.GetConfigurationAsync(cancellationToken);
        var result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudience = _clientId,
            IssuerSigningKeys = keys.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        });
        if (!result.IsValid)
        {
            // The token itself is never logged.
            _logger.LogInformation("Google ID token rejected. Reason={Reason}", result.Exception?.GetType().Name);
            return null;
        }

        var claims = result.ClaimsIdentity;
        var subject = claims.FindFirst("sub")?.Value;
        var email = claims.FindFirst("email")?.Value;
        var emailVerified = string.Equals(claims.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email) || !emailVerified)
            return null;

        return new GoogleIdentity(subject, email, claims.FindFirst("name")?.Value);
    }
}
