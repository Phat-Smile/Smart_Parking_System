using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Parking;
using SmartParking.Contracts.Parking.Availability.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

public static class ParkingClientRegistration
{
    public static IServiceCollection AddParkingServiceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ParkingServiceOptions.SectionName);
        services.AddOptions<ParkingServiceOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.GrpcEndpoint) &&
                                 Uri.TryCreate(options.GrpcEndpoint, UriKind.Absolute, out _),
                "ParkingService:GrpcEndpoint must be a valid absolute URI.")
            .Validate(options => options.TimeoutSeconds > 0,
                "ParkingService:TimeoutSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ParkingServiceOptions>>().Value;
            return GrpcChannel.ForAddress(options.GrpcEndpoint);
        });

        services.AddSingleton(sp =>
        {
            var channel = sp.GetRequiredService<GrpcChannel>();
            return new ParkingAvailabilityContract.ParkingAvailabilityContractClient(channel);
        });

        services.AddScoped<IParkingAvailabilityClient, ParkingAvailabilityGrpcClient>();
        services.AddScoped<IParkingLotCapacityProvider, UnconfiguredParkingLotCapacityProvider>();
        services.AddScoped<IParkingPricingProvider, UnconfiguredParkingPricingProvider>();

        return services;
    }
}
