using System;
using FluentValidation;
using ParkingSystem.Parking.Application.Common.Constants;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;

public sealed class ConfigureReservationAvailabilityPolicyCommandValidator : AbstractValidator<ConfigureReservationAvailabilityPolicyCommand>
{
    public ConfigureReservationAvailabilityPolicyCommandValidator()
    {
        RuleFor(x => x.LotId)
            .NotEmpty()
            .WithErrorCode(ParkingErrors.ReservationAvailabilityPolicy.LotIdEmpty.Code)
            .WithMessage(ParkingErrors.ReservationAvailabilityPolicy.LotIdEmpty.Message);

        RuleFor(x => x.ReservationEnabled)
            .NotNull()
            .WithErrorCode(ParkingErrors.ReservationAvailabilityPolicy.ReservationEnabledRequired.Code)
            .WithMessage(ParkingErrors.ReservationAvailabilityPolicy.ReservationEnabledRequired.Message);
    }
}
