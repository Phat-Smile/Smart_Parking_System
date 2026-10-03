using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ParkingSystem.AspNetCore;

internal static partial class SafeExceptionDiagnostics
{
    private static readonly Regex CredentialAssignment = new(
        @"(?i)(password|passwd|pwd|secret|token|connection\s*string|private[-_ ]?key|api[-_ ]?key)\s*[:=]\s*[^;\s,]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BearerCredential = new(
        @"(?i)(bearer\s+)[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UriCredential = new(
        @"(?i)(amqps?://)[^/@\s]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Format(Exception exception, IConfiguration configuration)
    {
        try
        {
            var details = exception.ToString();
            foreach (var (key, value) in configuration.AsEnumerable())
            {
                if (!string.IsNullOrWhiteSpace(value) && value.Length >= 4 && IsSensitiveKey(key))
                    details = details.Replace(value, "[redacted]", StringComparison.OrdinalIgnoreCase);
            }

            details = CredentialAssignment.Replace(details, "$1=[redacted]");
            details = BearerCredential.Replace(details, "$1[redacted]");
            details = UriCredential.Replace(details, "$1[redacted]@");
            details = details.ReplaceLineEndings(" ");
            return details.Length <= 4000 ? details : details[..4000];
        }
        catch
        {
            return exception.GetType().FullName ?? exception.GetType().Name;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("credential", StringComparison.OrdinalIgnoreCase);
    }
}
