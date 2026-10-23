using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Options;

namespace ParkingSystem.Booking.WebAPI.Configurations;

public static class ReservationOptionsRegistration
{
    /// <summary>
    /// Binds, validates, and registers <see cref="ReservationOptions"/> with eager validation on application startup.
    /// Resides in Booking.WebAPI as part of the application composition root.
    /// </summary>
    public static IServiceCollection AddReservationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ReservationOptions.SectionName);

        services.AddOptions<ReservationOptions>()
            .Bind(section)
            .Validate(options => options.DepositAmountVnd >= 0,
                "Reservation:DepositAmountVnd must not be negative.")
            .Validate(options => options.DepositAmountVnd % 1m == 0m,
                "Reservation:DepositAmountVnd must be an integer VND value.")
            .Validate(options => !options.HoldWhileDrivingDepositVnd.HasValue || options.HoldWhileDrivingDepositVnd.Value >= 0,
                "Reservation:HoldWhileDrivingDepositVnd must not be negative.")
            .Validate(options => !options.HoldWhileDrivingDepositVnd.HasValue || options.HoldWhileDrivingDepositVnd.Value % 1m == 0m,
                "Reservation:HoldWhileDrivingDepositVnd must be an integer VND value.")
            .ValidateOnStart();

        return services;
    }
}
