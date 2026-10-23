using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal static class ParkingLayoutRegistration
{
    public static IServiceCollection AddParkingLayout(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISlotDesignator, SlotDesignator>();
        services.AddScoped<IMotorbikeZoneCounter, MotorbikeZoneCounter>();
        services.AddScoped<IZoneLabelReader, ZoneLabelReader>();

        var options = ReadAvailabilityOptions(configuration.GetSection(AvailabilityOptions.SectionName));
        services.AddSingleton(options);

        if (string.Equals(options.Provider, AvailabilityOptions.StubProvider, StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IAvailableSlotReader, StubAvailableSlotReader>();
        else
            services.AddScoped<IAvailableSlotReader, AvailableSlotReader>();

        return services;
    }

    private static AvailabilityOptions ReadAvailabilityOptions(IConfigurationSection section)
    {
        var provider = section[nameof(AvailabilityOptions.Provider)] ?? AvailabilityOptions.DatabaseProvider;
        if (!string.Equals(provider, AvailabilityOptions.DatabaseProvider, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(provider, AvailabilityOptions.StubProvider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Availability:Provider '{provider}' is not supported. Use '{AvailabilityOptions.DatabaseProvider}' or '{AvailabilityOptions.StubProvider}'.");

        var stubCount = AvailabilityOptions.DefaultStubAvailableSlotCount;
        var configuredCount = section[nameof(AvailabilityOptions.StubAvailableSlotCount)];
        if (configuredCount is not null && (!int.TryParse(configuredCount, out stubCount) || stubCount < 0))
            throw new InvalidOperationException("Availability:StubAvailableSlotCount must be a whole number of 0 or more.");

        return new AvailabilityOptions { Provider = provider, StubAvailableSlotCount = stubCount };
    }
}
