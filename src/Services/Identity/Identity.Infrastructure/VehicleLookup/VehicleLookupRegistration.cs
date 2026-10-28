using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Vehicles.Lookup;

namespace ParkingSystem.Identity.Infrastructure.VehicleLookup;

public static class VehicleLookupRegistration
{
    public const string ProviderKey = "VehicleLookup:Provider";
    public const string StubProvider = "Stub";

    public static bool IsStubConfigured(IConfiguration configuration) =>
        string.Equals(configuration[ProviderKey], StubProvider, StringComparison.OrdinalIgnoreCase);

    // Hosts call this at startup so a deployment without an explicit provider never serves Stub data silently.
    public static void EnsureProviderConfigured(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[ProviderKey]))
            throw new InvalidOperationException(
                $"{ProviderKey} must be configured. Use '{StubProvider}' only for development or an intentional demo.");
    }

    public const string DatabaseProvider = "Database";

    // AddPersistence registers DbVehicleLookupReader when provider = "Database"; this method registers Stub.
    public static IServiceCollection AddVehicleLookupReader(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[ProviderKey] ?? StubProvider;

        if (string.Equals(provider, DatabaseProvider, StringComparison.OrdinalIgnoreCase))
            return services; // registered by AddPersistence

        if (!string.Equals(provider, StubProvider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{ProviderKey} '{provider}' is not supported. Supported providers: {StubProvider}, {DatabaseProvider}.");

        services.AddSingleton<IVehicleLookupReader, StubVehicleLookupReader>();
        return services;
    }
}
