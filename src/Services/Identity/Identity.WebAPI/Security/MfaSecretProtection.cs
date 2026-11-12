using Microsoft.AspNetCore.DataProtection;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Infrastructure.Security;

namespace ParkingSystem.Identity.WebAPI.Security;

public sealed class MfaSecretProtectionOptions
{
    public const string SectionName = "Security:DataProtection";

    // Thư mục giữ key ring; trong Docker phải là volume, mất key ring là mọi secret TOTP đã lưu không giải mã được nữa.
    public string? KeysPath { get; set; }
}

internal sealed class DataProtectionMfaSecretProtector(IDataProtectionProvider provider) : IMfaSecretProtector
{
    // Đổi purpose cũng làm mọi secret đã lưu không giải mã được.
    private readonly IDataProtector _protector = provider.CreateProtector("Identity.Mfa.TotpSecret");

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}

internal static class MfaSecretProtection
{
    public static IServiceCollection AddMfa(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("ParkingSystem.Identity");
        var keysPath = configuration[$"{MfaSecretProtectionOptions.SectionName}:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IMfaSecretProtector, DataProtectionMfaSecretProtector>();
        return services;
    }
}
