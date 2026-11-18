using FluentValidation;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;

public sealed class CancelForNoSuitableSlotCommandValidator : AbstractValidator<CancelForNoSuitableSlotCommand>
{
    public CancelForNoSuitableSlotCommandValidator()
    {
        RuleFor(c => c.LotId).NotEmpty().WithMessage("LotId không được để trống.");
        RuleFor(c => c.PlateNumber).NotEmpty().WithMessage("PlateNumber không được để trống.")
            .MaximumLength(Reservation.PlateNumberMaxLength);
        RuleFor(c => c.ArrivedAt).NotEmpty().WithMessage("ArrivedAt không được để trống.");
    }
}
