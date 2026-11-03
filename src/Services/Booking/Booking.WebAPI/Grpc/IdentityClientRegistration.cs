using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Vehicles;
using SmartParking.Contracts.Identity.Vehicle.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// Service collection extensions for registering the Identity Service gRPC client and typed adapter.
/// </summary>
public static class IdentityClientRegistration
{
    public static IServiceCollection AddIdentityServiceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(IdentityServiceOptions.SectionName);
        services.AddOptions<IdentityServiceOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.GrpcEndpoint) &&
                                 Uri.TryCreate(options.GrpcEndpoint, UriKind.Absolute, out var uri) &&
                                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "IdentityService:GrpcEndpoint must be a valid absolute HTTP or HTTPS URI.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 30,
                "IdentityService:TimeoutSeconds must be between 1 and 30.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            var channel = GrpcChannel.ForAddress(options.GrpcEndpoint);
            return new IdentityVehicleContract.IdentityVehicleContractClient(channel);
        });

        services.AddScoped<IVehicleOwnershipClient, IdentityVehicleGrpcClient>();

        return services;
    }
}

