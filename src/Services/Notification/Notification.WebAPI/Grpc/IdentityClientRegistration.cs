using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Notification.Application.Common.Identity;
using SmartParking.Contracts.Identity.Staff.V1;

namespace ParkingSystem.Notification.WebAPI.Grpc;

public static class IdentityClientRegistration
{
    public static IServiceCollection AddNotificationIdentityServiceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IdentityServiceOptions>()
            .Bind(configuration.GetSection(IdentityServiceOptions.SectionName));

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            var endpoint = !string.IsNullOrWhiteSpace(options.GrpcEndpoint) ? options.GrpcEndpoint : "http://localhost:5001";
            return new IdentityStaffContract.IdentityStaffContractClient(GrpcChannel.ForAddress(endpoint));
        });

        services.AddScoped<IUserAccountEmailLookup, IdentityAccountEmailLookupClient>();

        return services;
    }
}
