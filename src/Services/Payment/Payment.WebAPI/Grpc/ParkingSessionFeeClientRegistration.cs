using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Interfaces;
using SmartParking.Contracts.Parking.Membership.V1;
using SmartParking.Contracts.Parking.SessionFee.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

public static class ParkingSessionFeeClientRegistration
{
    public static IServiceCollection AddParkingSessionFeeClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ParkingServiceOptions>()
            .Bind(configuration.GetSection(ParkingServiceOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.GrpcEndpoint, UriKind.Absolute, out _),
                "ParkingService:GrpcEndpoint must be a valid absolute URI.")
            .Validate(o => o.TimeoutSeconds > 0,
                "ParkingService:TimeoutSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
            GrpcChannel.ForAddress(sp.GetRequiredService<IOptions<ParkingServiceOptions>>().Value.GrpcEndpoint));
        services.AddSingleton(sp =>
            new ParkingSessionFeeContract.ParkingSessionFeeContractClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IParkingSessionGateway, ParkingSessionFeeGrpcGateway>();
        services.AddSingleton(sp =>
            new ParkingLotMembershipContract.ParkingLotMembershipContractClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IParkingLotMembershipGateway, ParkingLotMembershipGrpcGateway>();

        return services;
    }
}
