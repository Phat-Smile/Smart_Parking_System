using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Vehicles.Lookup;

namespace ParkingSystem.Identity.Infrastructure.VehicleLookup;

public static class VehicleLookupRegistration
{
    public const string ProviderKey = "VehicleLookup:Provider";
    public const string StubProvider = "Stub";

    // Stub is the only provider until US-VEH-001/002 adds the PostgreSQL reader; then only this switch changes.
    public static IServiceCollection AddVehicleLookupReader(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[ProviderKey] ?? StubProvider;

        if (!string.Equals(provider, StubProvider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{ProviderKey} '{provider}' is not supported. Supported providers: {StubProvider}.");

        services.AddSingleton<IVehicleLookupReader, StubVehicleLookupReader>();
        return services;
    }
}
