using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Payments;
using SmartParking.Contracts.Payment.Reservation.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// Service collection extensions for registering the Payment Service gRPC client and typed adapter.
/// </summary>
public static class PaymentClientRegistration
{
    public static IServiceCollection AddPaymentServiceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PaymentServiceOptions.SectionName);
        services.AddOptions<PaymentServiceOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.GrpcEndpoint) &&
                                 Uri.TryCreate(options.GrpcEndpoint, UriKind.Absolute, out var uri) &&
                                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "PaymentService:GrpcEndpoint must be a valid absolute HTTP or HTTPS URI.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 30,
                "PaymentService:TimeoutSeconds must be between 1 and 30.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PaymentServiceOptions>>().Value;
            var channel = GrpcChannel.ForAddress(options.GrpcEndpoint);
            return new ReservationWalletContract.ReservationWalletContractClient(channel);
        });

        services.AddScoped<IReservationPaymentClient, ReservationPaymentGrpcClient>();

        return services;
    }
}

