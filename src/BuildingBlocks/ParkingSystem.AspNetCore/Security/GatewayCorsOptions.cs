namespace ParkingSystem.AspNetCore.Security;

public sealed class GatewayCorsOptions
{
    public const string SectionName = "Security:Cors";

    // Comma-separated explicit origins. An empty value intentionally allows no browser origins.
    public string AllowedOrigins { get; set; } = string.Empty;

    public static string[] ParseAllowedOrigins(string? configuredOrigins) =>
        (configuredOrigins ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool IsValidOrigin(string origin)
    {
        if (origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               uri.UserInfo.Length == 0 &&
               uri.AbsolutePath == "/" &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment) &&
               origin.TrimEnd('/').Equals(uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }
}
