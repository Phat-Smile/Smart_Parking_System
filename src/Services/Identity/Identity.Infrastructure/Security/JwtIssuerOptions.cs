namespace ParkingSystem.Identity.Infrastructure.Security;

public sealed class JwtIssuerOptions
{
    public const string SectionName = "Security:Jwt";

    public string Issuer { get; set; } = "ParkingSystem.Identity";
    public string Audience { get; set; } = "ParkingSystem";
    public string PrivateKeyPath { get; set; } = ".local/security/jwt/private.pem";
    public int ExpirationMinutes { get; set; } = 15;
}
