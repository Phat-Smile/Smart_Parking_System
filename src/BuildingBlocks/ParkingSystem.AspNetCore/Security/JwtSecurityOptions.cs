namespace ParkingSystem.AspNetCore.Security;

public sealed class JwtSecurityOptions
{
    public const string SectionName = "Security:Jwt";

    public string Issuer { get; set; } = "ParkingSystem.Identity";
    public string Audience { get; set; } = "ParkingSystem";
    public string PublicKeyPath { get; set; } = ".local/security/jwt/public.pem";
    public int ClockSkewSeconds { get; set; } = 30;
}
