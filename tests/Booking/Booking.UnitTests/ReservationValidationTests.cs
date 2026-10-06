using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;
using ParkingSystem.Booking.Application.Reservations.Errors;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Tests.Booking.UnitTests.Fakes;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class ReservationValidationTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly TestClock _clock;
    private readonly HoldReservationCapacityCommandValidator _validator;
    private readonly HoldReservationCapacityCommandHandler _handler;

    public ReservationValidationTests()
    {
        _clock = new TestClock(_baseTime);
        _validator = new HoldReservationCapacityCommandValidator(_clock);
        _handler = new HoldReservationCapacityCommandHandler(
            new InMemoryReservationRepository(),
            new InMemoryReservationCapacityCoordinator(),
            new FakeCapacityProvider(),
            _clock);
    }

    [Fact]
    public void Validator_rejects_motorbike_for_reservation()
    {
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "59-T1 12345",
            Guid.NewGuid(),
            VehicleGroup.Motorbike,
            ReservationType.Scheduled,
            _baseTime.AddHours(2),
            60,
            20000);

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(command.VehicleGroup));
    }

    [Fact]
    public void Validator_rejects_scheduled_duration_exceeding_24_hours()
    {
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-123.45",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddHours(2),
            1441, // > 1440 minutes
            50000);

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(command.DurationMinutes));
    }

    [Fact]
    public void Validator_rejects_scheduled_booking_more_than_7_days_in_advance()
    {
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-123.45",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(8), // > 7 days
            60,
            50000);

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(command.AppointmentTime));
    }

    [Fact]
    public async Task Immediate_hold_fails_when_no_physical_available_slots()
    {
        // Hold window starts immediately (appointment in 10 minutes, buffer is 30 minutes, so window has started)
        var targetTime = _baseTime.AddMinutes(10);
        // Valid capacity = 10, but AvailableSlotCount = 0 (physical lot full right now)
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 10, AvailableSlotCount: 0, ReservationAllocationRatio: 0.5m);

        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-123.45",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            targetTime,
            null,
            20000,
            CapacitySnapshot: snapshot);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.PhysicalSlotsUnavailable.Code, result.Error!.Code);
    }

    private sealed class FakeCapacityProvider : IParkingLotCapacityProvider
    {
        public Task<ParkingLotCapacitySnapshot?> GetCapacitySnapshotAsync(
            Guid lotId,
            VehicleGroup vehicleGroup,
            DateTimeOffset evaluationTime,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ParkingLotCapacitySnapshot?>(new ParkingLotCapacitySnapshot(20, 10, 0.50m));
        }
    }
}
