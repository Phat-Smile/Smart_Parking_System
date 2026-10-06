using FluentValidation;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;

public sealed class HoldReservationCapacityCommandValidator : AbstractValidator<HoldReservationCapacityCommand>
{
    public HoldReservationCapacityCommandValidator(IClock clock)
    {
        RuleFor(command => command.ReservationId)
            .NotEmpty().WithMessage("Reservation id cannot be empty.");

        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("User id cannot be empty.");

        RuleFor(command => command.VehicleId)
            .NotEmpty().WithMessage("Vehicle id cannot be empty.");

        RuleFor(command => command.PlateNumber)
            .NotEmpty().WithMessage("Plate number cannot be empty.")
            .MaximumLength(20).WithMessage("Plate number cannot exceed 20 characters.");

        RuleFor(command => command.LotId)
            .NotEmpty().WithMessage("Lot id cannot be empty.");

        RuleFor(command => command.VehicleGroup)
            .Equal(VehicleGroup.Car).WithMessage("Only passenger cars (Car) can reserve parking capacity (BR-01.a).");

        RuleFor(command => command.PrepaidAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Prepaid amount cannot be negative.");

        When(command => command.ReservationType == ReservationType.Scheduled, () =>
        {
            RuleFor(command => command.DurationMinutes)
                .NotNull().WithMessage("Duration is required for scheduled reservations.")
                .GreaterThan(0).WithMessage("Duration must be greater than zero.")
                .LessThanOrEqualTo(1440).WithMessage("Scheduled duration cannot exceed 24 hours (1440 minutes) (BR-01.a).");

            RuleFor(command => command.AppointmentTime)
                .Must(appointmentTime => appointmentTime <= clock.UtcNow.AddDays(7).AddMinutes(5))
                .WithMessage("Scheduled reservations cannot be booked more than 7 days in advance (BR-01.a).");
        });
    }
}
