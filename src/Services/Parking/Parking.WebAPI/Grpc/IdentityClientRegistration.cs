using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Parking.Application.Common.Identity;
using SmartParking.Contracts.Identity.Staff.V1;
using SmartParking.Contracts.Identity.Vehicle.V1;

namespace ParkingSystem.Parking.WebAPI.Grpc;

public static class IdentityClientRegistration
{
    public static IServiceCollection AddIdentityServiceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IdentityServiceOptions>()
            .Bind(configuration.GetSection(IdentityServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.GrpcEndpoint, UriKind.Absolute, out var endpoint) &&
                                 (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps),
                "IdentityService:GrpcEndpoint must be an absolute http or https URI.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 30,
                "IdentityService:TimeoutSeconds must be between 1 and 30.")
            .ValidateOnStart();

        // One channel for the whole process, owned by the typed client; it is not registered on its own so a later
        // client for another service (Booking) cannot be mixed up with it.
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            return new IdentityVehicleContract.IdentityVehicleContractClient(GrpcChannel.ForAddress(options.GrpcEndpoint));
        });

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            return new IdentityStaffContract.IdentityStaffContractClient(GrpcChannel.ForAddress(options.GrpcEndpoint));
        });

        services.AddScoped<IPlateOwnerLookup, IdentityVehicleGrpcClient>();
        services.AddScoped<IStaffAccountLookup, IdentityStaffGrpcClient>();

        return services;
    }
}
