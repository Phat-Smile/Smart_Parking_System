using FluentValidation;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;

public sealed class ProcessSlotSensorEventCommandValidator : AbstractValidator<ProcessSlotSensorEventCommand>
{
    public ProcessSlotSensorEventCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("DeviceId is required and must not exceed 64 characters.");

        RuleFor(x => x.LotId)
            .NotEmpty()
            .WithMessage("LotId is required.");

        RuleFor(x => x.SlotCode)
            .NotEmpty()
            .MaximumLength(ParkingSlot.SlotCodeMaxLength)
            .WithMessage($"SlotCode is required and must not exceed {ParkingSlot.SlotCodeMaxLength} characters.");

        RuleFor(x => x.State)
            .IsInEnum()
            .WithMessage("SensorState must be a valid enum value (Empty or Occupied).");

        RuleFor(x => x.EventTime)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("EventTime must be provided by the device.");
    }
}
