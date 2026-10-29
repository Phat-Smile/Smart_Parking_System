using FluentValidation;
using ParkingSystem.Parking.Application.Common.Constants;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;

public sealed class ConfigureReservationRatioPolicyCommandValidator : AbstractValidator<ConfigureReservationRatioPolicyCommand>
{
    public ConfigureReservationRatioPolicyCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.LotId)
            .NotEmpty()
            .WithMessage(ParkingErrors.ReservationRatioPolicy.LotIdEmpty.Message);

        RuleFor(x => x.ReservationRatioPercent)
            .NotNull()
            .WithMessage(ParkingErrors.ReservationRatioPolicy.RatioRequired.Message)
            .InclusiveBetween(0, 100)
            .WithMessage(ParkingErrors.ReservationRatioPolicy.RatioOutOfRange.Message);
    }
}
