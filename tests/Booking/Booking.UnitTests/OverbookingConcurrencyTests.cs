using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;
using ParkingSystem.Booking.Application.Reservations.Errors;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Tests.Booking.UnitTests.Fakes;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class OverbookingConcurrencyTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly TestClock _clock;
    private readonly InMemoryReservationRepository _repository;
    private readonly InMemoryReservationCapacityCoordinator _coordinator;
    private readonly HoldReservationCapacityCommandHandler _handler;

    public OverbookingConcurrencyTests()
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
    public async Task Test_A_100_concurrent_requests_for_one_remaining_capacity_results_in_exactly_one_success()
    {
        var lotId = Guid.NewGuid();
        // valid capacity = 2, ratio = 0.5 => ceiling = 1
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 2, AvailableSlotCount: 2, ReservationAllocationRatio: 0.50m);
        var targetTime = _baseTime.AddHours(2);
        const int concurrentRequests = 100;

        var tasks = Enumerable.Range(1, concurrentRequests).Select(i =>
        {
            var command = new HoldReservationCapacityCommand(
                Guid.NewGuid(),
                Guid.NewGuid(), // distinct users to avoid account abuse limit
                Guid.NewGuid(),
                $"51A-CON.{i:D3}", // distinct plates
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                targetTime,
                60,
                50000,
                CapacitySnapshot: snapshot);

            return Task.Run(() => _handler.Handle(command, CancellationToken.None));
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => r.IsFailure);

        Assert.Equal(1, successes);
        Assert.Equal(99, failures);

        // Verify remaining capacity is 0 (ceiling = 1, active = 1)
        var window = ReservationTimeWindow.ForReservation(ReservationType.Scheduled, targetTime, 60);
        var activeCount = await _repository.CountActiveOverlappingReservationsAsync(
            lotId,
            VehicleGroup.Car,
            window,
            _clock.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Test_B_Capacity_zero_rejects_all_reservation_attempts()
    {
        var lotId = Guid.NewGuid();
        // valid capacity = 0 or ratio = 0 => ceiling = 0
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 0, AvailableSlotCount: 0, ReservationAllocationRatio: 0.50m);
        var targetTime = _baseTime.AddHours(2);

        var command = new HoldReservationCapacityCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-123.45",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            targetTime,
            60,
            50000,
            CapacitySnapshot: snapshot);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.ReservationCeilingExceeded.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Test_C_Capacity_N_with_more_than_N_concurrent_requests_allows_exactly_N_successes()
    {
        var lotId = Guid.NewGuid();
        const int capacityN = 5;
        // valid capacity = 10, ratio = 0.5 => ceiling = 5
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 10, AvailableSlotCount: 10, ReservationAllocationRatio: 0.50m);
        var targetTime = _baseTime.AddHours(2);
        const int concurrentRequests = 30;

        var tasks = Enumerable.Range(1, concurrentRequests).Select(i =>
        {
            var command = new HoldReservationCapacityCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"51A-CAP.{i:D3}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                targetTime,
                60,
                50000,
                CapacitySnapshot: snapshot);

            return Task.Run(() => _handler.Handle(command, CancellationToken.None));
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => r.IsFailure);

        Assert.Equal(capacityN, successes);
        Assert.Equal(concurrentRequests - capacityN, failures);
    }

    [Fact]
    public async Task Test_D_Remaining_capacity_never_becomes_negative()
    {
        var lotId = Guid.NewGuid();
        // Ceiling = 3
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 6, AvailableSlotCount: 6, ReservationAllocationRatio: 0.50m);
        var targetTime = _baseTime.AddHours(2);
        const int concurrentRequests = 50;

        var tasks = Enumerable.Range(1, concurrentRequests).Select(i =>
        {
            var command = new HoldReservationCapacityCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"51A-NEG.{i:D3}",
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                targetTime,
                60,
                50000,
                CapacitySnapshot: snapshot);

            return Task.Run(() => _handler.Handle(command, CancellationToken.None));
        }).ToList();

        await Task.WhenAll(tasks);

        var window = ReservationTimeWindow.ForReservation(ReservationType.Scheduled, targetTime, 60);
        var activeCount = await _repository.CountActiveOverlappingReservationsAsync(
            lotId,
            VehicleGroup.Car,
            window,
            _clock.UtcNow,
            CancellationToken.None);

        var ceiling = ReservationCapacityPolicy.CalculateReservationCeiling(snapshot.ValidCapacity, snapshot.ReservationAllocationRatio);
        var remaining = ceiling - activeCount;

        Assert.True(remaining >= 0, $"Remaining capacity must never be negative, was {remaining}");
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Test_E_Duplicate_command_with_same_idempotency_key_does_not_consume_capacity_twice()
    {
        var lotId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var plateNumber = "51A-777.77";
        var targetTime = _baseTime.AddHours(2);

        // Ceiling = 1
        var snapshot = new ParkingLotCapacitySnapshot(ValidCapacity: 2, AvailableSlotCount: 2, ReservationAllocationRatio: 0.50m);

        var command = new HoldReservationCapacityCommand(
            reservationId,
            userId,
            vehicleId,
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            targetTime,
            60,
            50000,
            CapacitySnapshot: snapshot);

        // Initial request succeeds
        var firstResult = await _handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Retry with same ReservationId (idempotency key)
        var retryResult = await _handler.Handle(command, CancellationToken.None);
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(firstResult.Value!.ReservationId, retryResult.Value!.ReservationId);

        // Active reservation count in repository must still be 1 (not 2)
        var allReservations = _repository.GetAll();
        Assert.Single(allReservations);
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
