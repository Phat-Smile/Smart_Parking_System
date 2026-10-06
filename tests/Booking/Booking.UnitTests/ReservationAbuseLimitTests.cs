using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;
using ParkingSystem.Booking.Application.Reservations.Errors;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Tests.Booking.UnitTests.Fakes;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class ReservationAbuseLimitTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly TestClock _clock;
    private readonly InMemoryReservationRepository _repository;
    private readonly InMemoryReservationCapacityCoordinator _coordinator;
    private readonly HoldReservationCapacityCommandHandler _handler;

    public ReservationAbuseLimitTests()
    {
        _clock = new TestClock(_baseTime);
        _repository = new InMemoryReservationRepository();
        _coordinator = new InMemoryReservationCapacityCoordinator();
        _handler = new HoldReservationCapacityCommandHandler(
            _repository,
            _coordinator,
            new FakeCapacityProvider(),
            _clock);
    }

    [Fact]
    public async Task Test_F_Same_plate_with_overlapping_active_reservation_is_rejected()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51A-123.45";

        // Existing active reservation: [13:30, 16:00)
        var existingAppointment = _baseTime.AddHours(2); // 14:00
        var existing = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            userId,
            vehicleId,
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            existingAppointment,
            120, // 2 hours -> window [13:30, 16:00)
            50000,
            _baseTime.AddMinutes(10),
            _baseTime);
        _repository.Add(existing);

        // Attempt new reservation for same plate with overlapping window: 15:00 (+1 hour overlap)
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            vehicleId,
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddHours(3), // 15:00 -> window [14:30, 17:00)
            120,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.PlateOverlappingReservationExists.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Test_G_Same_plate_with_non_overlapping_reservation_is_allowed()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51A-123.45";

        // Existing active reservation: 14:00 with 1h duration -> [13:30, 15:00)
        var existing = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            userId,
            vehicleId,
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddHours(2), // 14:00
            60,
            50000,
            _baseTime.AddMinutes(10),
            _baseTime);
        _repository.Add(existing);

        // New reservation non-overlapping: 18:00 with 1h duration -> [17:30, 19:00)
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            vehicleId,
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddHours(6), // 18:00
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_H_Account_with_exactly_3_active_reservations_rejects_next_reservation()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create 3 active reservations for different vehicles / non-overlapping times
        for (var i = 1; i <= 3; i++)
        {
            var res = Reservation.CreateTemporaryHold(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"51A-000.0{i}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                _baseTime.AddDays(i),
                60,
                50000,
                _baseTime.AddMinutes(10),
                _baseTime);
            _repository.Add(res);
        }

        // 4th reservation attempt
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-999.99",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(4),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.AccountActiveLimitExceeded.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Test_I_Account_with_2_active_reservations_allows_next_valid_reservation()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (var i = 1; i <= 2; i++)
        {
            var res = Reservation.CreateTemporaryHold(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"51A-000.0{i}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                _baseTime.AddDays(i),
                60,
                50000,
                _baseTime.AddMinutes(10),
                _baseTime);
            _repository.Add(res);
        }

        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-999.99",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(3),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_J_Cancelled_reservation_does_not_count_toward_active_limit()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // 3 reservations created, but 1 is cancelled
        for (var i = 1; i <= 2; i++)
        {
            var active = Reservation.CreateTemporaryHold(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"51A-000.0{i}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                _baseTime.AddDays(i),
                60,
                50000,
                _baseTime.AddMinutes(10),
                _baseTime);
            _repository.Add(active);
        }

        var cancelled = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-000.03",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(3),
            60,
            50000,
            _baseTime.AddMinutes(10),
            _baseTime);
        cancelled.Cancel(CancelReason.CustomerCancelled, _baseTime.AddMinutes(2));
        _repository.Add(cancelled);

        // Account currently only has 2 active reservations (since 1 is cancelled). 3rd active is allowed!
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-999.99",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(4),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_K_Expired_reservation_does_not_count_toward_active_limit()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (var i = 1; i <= 2; i++)
        {
            var active = Reservation.CreateTemporaryHold(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"51A-000.0{i}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                _baseTime.AddDays(i),
                60,
                50000,
                _baseTime.AddMinutes(10),
                _baseTime);
            _repository.Add(active);
        }

        var expired = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-000.03",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(3),
            60,
            50000,
            _baseTime.AddMinutes(10),
            _baseTime);
        expired.Expire(_baseTime.AddMinutes(11));
        _repository.Add(expired);

        // 3rd active reservation should be accepted
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-999.99",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(4),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_L_Used_reservation_does_not_count_toward_active_limit()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (var i = 1; i <= 2; i++)
        {
            var active = Reservation.CreateTemporaryHold(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"51A-000.0{i}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                _baseTime.AddDays(i),
                60,
                50000,
                _baseTime.AddMinutes(10),
                _baseTime);
            _repository.Add(active);
        }

        var used = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-000.03",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddHours(1),
            60,
            50000,
            _baseTime.AddMinutes(10),
            _baseTime);
        used.Confirm(_baseTime.AddMinutes(5));
        used.MarkUsed(Guid.NewGuid(), _baseTime.AddHours(1));
        _repository.Add(used);

        // Vehicle has entered gate, so it is no longer an active reservation hold
        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-999.99",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(4),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_O_One_account_with_multiple_vehicles_allowed_under_limit_and_no_overlap()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Vehicle 1
        var cmd1 = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-111.11",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(1),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));
        var res1 = await _handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        // Vehicle 2
        var cmd2 = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-222.22",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(2),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));
        var res2 = await _handler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsSuccess);

        // Vehicle 3
        var cmd3 = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "51A-333.33",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _baseTime.AddDays(3),
            60,
            50000,
            CapacitySnapshot: new(ValidCapacity: 20, AvailableSlotCount: 10, ReservationAllocationRatio: 0.5m));
        var res3 = await _handler.Handle(cmd3, CancellationToken.None);
        Assert.True(res3.IsSuccess);
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
