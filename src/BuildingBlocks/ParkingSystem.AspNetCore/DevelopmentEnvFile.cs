using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ParkingSystem.AspNetCore;

// Development only: a service started from the IDE reads the local credentials from the repository .env file, the
// same file docker compose reads, so no password has to live in appsettings. Containers never see .env
// (.dockerignore) and get these values from compose. A value already present in configuration always wins.
internal static class DevelopmentEnvFile
{
    public const string FileName = ".env";

    public static void AddTo(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
            return;

        var path = Find(builder.Environment.ContentRootPath) ?? Find(AppContext.BaseDirectory);
        if (path is null)
            return;

        var values = ToConfiguration(Parse(File.ReadAllLines(path)));
        // appsettings.json declares empty placeholders (ConnectionStrings:DefaultConnection = ""): empty counts as missing.
        var missing = values.Where(pair => string.IsNullOrEmpty(builder.Configuration[pair.Key])).ToArray();
        if (missing.Length > 0)
            builder.Configuration.AddInMemoryCollection(missing);
    }

    internal static Dictionary<string, string?> ToConfiguration(IReadOnlyDictionary<string, string> env)
    {
        // Raw keys stay available as they are (SMTP_HOST, GOOGLE_CLIENT_ID, ...), like environment variables.
        var values = env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase);
        string Get(string key, string fallback) => env.TryGetValue(key, out var value) && value.Length > 0 ? value : fallback;

        // The IDE reaches the compose containers through their published localhost ports.
        if (env.TryGetValue("DATABASE_CONNECTION_STRING", out var sharedConnection) &&
            !string.IsNullOrWhiteSpace(sharedConnection))
        {
            values["ConnectionStrings:DefaultConnection"] = sharedConnection;
        }
        else if (env.ContainsKey("POSTGRES_USER") && env.ContainsKey("POSTGRES_PASSWORD"))
        {
            values["ConnectionStrings:DefaultConnection"] =
                $"Host=127.0.0.1;Port={Get("POSTGRES_HOST_PORT", "5433")};Database={Get("POSTGRES_DB", "parking_system")};" +
                $"Username={env["POSTGRES_USER"]};Password={env["POSTGRES_PASSWORD"]};GSS Encryption Mode=Disable";
        }

        if (env.ContainsKey("RABBITMQ_USER") && env.ContainsKey("RABBITMQ_PASSWORD"))
        {
            values["RabbitMq:Host"] = "127.0.0.1";
            values["RabbitMq:Port"] = Get("RABBITMQ_HOST_PORT", "5672");
            values["RabbitMq:Username"] = env["RABBITMQ_USER"];
            values["RabbitMq:Password"] = env["RABBITMQ_PASSWORD"];
            values["RabbitMq:VirtualHost"] = Get("RABBITMQ_VHOST", "/");
        }

        if (env.TryGetValue("SECURITY_OTP_PEPPER", out var pepper) && pepper.Length > 0)
            values["Security:Otp:Pepper"] = pepper;

        return values;
    }

    internal static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;

            var value = trimmed[(separator + 1)..].Trim();
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
                value = value[1..^1];
            env[trimmed[..separator].Trim()] = value;
        }

        return env;
    }

    private static string? Find(string start)
    {
        for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, FileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
