using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OpenParkingSessionTests
{
    private static readonly Guid LotId = Guid.Parse("8f3c2a10-5b7e-4c1d-9a2e-0d6f1b2c3a40");
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 7, 30, 0, TimeSpan.Zero);
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly OpenParkingSessionCommandValidator validator = new();

    [Theory]
    [InlineData("Car")]
    [InlineData("motorbike")]
    public void Validator_accepts_known_vehicle_types(string vehicleType)
    {
        Assert.True(validator.Validate(new OpenParkingSessionCommand(LotId, "51A-123.45", vehicleType, StaffId)).IsValid);
    }

    [Fact]
    public void Validator_rejects_missing_lot()
    {
        var result = validator.Validate(new OpenParkingSessionCommand(Guid.Empty, "51A-123.45", "Car", StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OpenParkingSessionCommand.LotId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_rejects_missing_plate(string plate)
    {
        var result = validator.Validate(new OpenParkingSessionCommand(LotId, plate, "Car", StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OpenParkingSessionCommand.PlateNumber));
    }

    [Fact]
    public void Validator_rejects_plate_longer_than_max_length()
    {
        var plate = new string('A', ParkingSession.PlateNumberMaxLength + 1);

        var result = validator.Validate(new OpenParkingSessionCommand(LotId, plate, "Car", StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OpenParkingSessionCommand.PlateNumber));
    }

    [Theory]
    [InlineData("Truck")]
    [InlineData("5")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData(" Car ")]
    [InlineData("")]
    public void Validator_rejects_unknown_vehicle_type(string vehicleType)
    {
        var result = validator.Validate(new OpenParkingSessionCommand(LotId, "51A-123.45", vehicleType, StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OpenParkingSessionCommand.VehicleType));
    }

    [Fact]
    public async Task Handler_opens_active_unlinked_session_when_plate_has_no_owner()
    {
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(
            new OpenParkingSessionCommand(LotId, " 51a-123.45 ", "car", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var session = Assert.Single(repository.Saved);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal("51A-123.45", session.PlateNumber);
        Assert.Equal(VehicleType.Car, session.VehicleType);
        Assert.Equal(Now, session.EntryTime);
        Assert.Null(session.UserId);
        Assert.Null(session.VehicleId);
        Assert.False(result.Value!.IsAccountLinked);
        Assert.Equal("UnregisteredWalkIn", result.Value.CustomerType);
        Assert.Equal("Active", result.Value.Status);
        Assert.Null(result.Value.ReservationId);
        Assert.Equal(session.Id, result.Value.SessionId);
    }

    [Fact]
    public async Task Handler_links_account_and_vehicle_when_plate_has_owner()
    {
        var owner = new PlateOwner(Guid.NewGuid(), Guid.NewGuid());
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(owner), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(
            new OpenParkingSessionCommand(LotId, "51A-123.45", "Motorbike", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.Value!.IsAccountLinked);
        Assert.Equal("RegisteredWalkIn", result.Value.CustomerType);
        var session = Assert.Single(repository.Saved);
        Assert.Equal(owner.UserId, session.UserId);
        Assert.Equal(owner.VehicleId, session.VehicleId);
    }

    [Fact]
    public async Task Handler_rejects_plate_that_already_has_an_open_session()
    {
        var repository = new FakeParkingSessionRepository { HasOpenSession = true };
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(
            new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(OpenParkingSessionErrors.SessionAlreadyOpen, result.Error);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Handler_reports_already_open_when_concurrent_request_wins_the_unique_index()
    {
        var repository = new FakeParkingSessionRepository { SaveSucceeds = false };
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(
            new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(OpenParkingSessionErrors.SessionAlreadyOpen, result.Error);
    }

    [Fact]
    public async Task Handler_rejects_lot_that_does_not_exist_or_is_not_approved()
    {
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: false), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(
            new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(OpenParkingSessionErrors.LotNotFound, result.Error);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Handler_refuses_staff_not_assigned_to_the_lot_and_opens_nothing()
    {
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: false), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Handler_issues_a_different_qr_token_without_plate_for_each_session()
    {
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), new FakeMotorbikeZones(), new FakeUnitOfWork(), new FakeSlotDesignator(), new FakeHeldQuota(0), new FakeZoneLabels());

        var first = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-111.11", "Car", StaffId), TestContext.Current.CancellationToken);
        var second = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-222.22", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Value!.QrToken, second.Value!.QrToken);
        Assert.DoesNotContain("51A", first.Value.QrToken, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Motorbike_takes_one_zone_space_in_the_same_transaction_as_the_session()
    {
        var repository = new FakeParkingSessionRepository();
        var zones = new FakeMotorbikeZones();
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(repository, zones, unitOfWork);

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "59X1-234.56", "Motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(FakeMotorbikeZones.ZoneId, Assert.Single(repository.Saved).ZoneId);
        Assert.Equal(FakeMotorbikeZones.ZoneId, result.Value!.ZoneId);
        Assert.Equal(79, result.Value.AvailableMotorbikeSpaces);
        // Printed with the QR so the rider knows where to park (team decision 2026-10-22).
        Assert.Equal("M2", result.Value.ZoneCode);
        Assert.Equal("Tầng 2", result.Value.FloorName);
        Assert.Equal(1, zones.Taken);
        Assert.True(unitOfWork.Committed);
    }

    [Fact]
    public async Task Motorbike_is_refused_when_the_zone_is_full()
    {
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(repository, new FakeMotorbikeZones(full: true), unitOfWork);

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "59X1-234.56", "Motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.MotorbikeZoneFull, result.Error);
        Assert.Empty(repository.Added);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Motorbike_is_refused_when_the_lot_has_no_motorbike_zone()
    {
        var handler = NewHandler(new FakeParkingSessionRepository(), new FakeMotorbikeZones(configured: false), new FakeUnitOfWork());

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "59X1-234.56", "Motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.MotorbikeZoneNotConfigured, result.Error);
    }

    [Fact]
    public async Task Motorbike_space_is_not_committed_when_the_session_save_loses_the_unique_index()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(new FakeParkingSessionRepository { SaveSucceeds = false }, new FakeMotorbikeZones(), unitOfWork);

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "59X1-234.56", "Motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.SessionAlreadyOpen, result.Error);
        // Disposing without commit rolls the +1 back.
        Assert.False(unitOfWork.Committed);
        Assert.True(unitOfWork.Disposed);
    }

    [Fact]
    public async Task Car_does_not_touch_the_motorbike_zone()
    {
        var zones = new FakeMotorbikeZones();
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(new FakeParkingSessionRepository(), zones, unitOfWork);

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AvailableMotorbikeSpaces);
        Assert.Equal(0, zones.Taken);
    }

    // US-PARK-007: walk-in car entry.

    [Fact]
    public async Task Walk_in_car_gets_a_slot_and_its_code_in_the_same_transaction_as_the_session()
    {
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var slots = new FakeSlotDesignator(available: 3);
        var handler = NewHandler(repository, new FakeMotorbikeZones(), unitOfWork, slots, new FakeHeldQuota(2));

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var session = Assert.Single(repository.Saved);
        Assert.Equal(session.Id, slots.DesignatedFor);
        Assert.Equal(FakeSlotDesignator.CarZoneId, session.ZoneId);
        // BR-04.a: the driver is told the slot, with its zone and floor.
        Assert.Equal("A-01", result.Value!.SlotCode);
        Assert.Equal("CAR-A", result.Value.ZoneCode);
        Assert.Equal("Tầng 1", result.Value.FloorName);
        Assert.True(slots.Locked);
        Assert.True(unitOfWork.Committed);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 5)]
    [InlineData(0, 0)]
    public async Task Walk_in_car_is_refused_when_held_reservations_leave_no_walk_in_slot(int available, int held)
    {
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var slots = new FakeSlotDesignator(available);
        var handler = NewHandler(repository, new FakeMotorbikeZones(), unitOfWork, slots, new FakeHeldQuota(held));

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        // BR-04.h, AC 2: the barrier stays closed and the screen shows the message.
        Assert.Equal(OpenParkingSessionErrors.WalkInFull, result.Error);
        Assert.Equal("Hết chỗ cho xe không đặt trước.", result.Error!.Message);
        Assert.Null(slots.DesignatedFor);
        Assert.Empty(repository.Added);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Walk_in_car_is_refused_when_the_last_slot_changes_state_after_the_count()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(new FakeParkingSessionRepository(), new FakeMotorbikeZones(), unitOfWork, new FakeSlotDesignator(available: 1, slotLeft: false), new FakeHeldQuota(0));

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.WalkInFull, result.Error);
        Assert.False(unitOfWork.Committed);
    }

    [Fact]
    public async Task Walk_in_car_is_refused_without_opening_a_transaction_when_the_held_reservations_are_unknown()
    {
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var slots = new FakeSlotDesignator(available: 5);
        var handler = NewHandler(repository, new FakeMotorbikeZones(), unitOfWork, slots, new FakeHeldQuota(null));

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.HeldQuotaUnavailable, result.Error);
        Assert.False(unitOfWork.Begun);
        Assert.False(slots.Locked);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Walk_in_car_slot_is_not_committed_when_the_session_save_loses_the_unique_index()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = NewHandler(new FakeParkingSessionRepository { SaveSucceeds = false }, new FakeMotorbikeZones(), unitOfWork, new FakeSlotDesignator(), new FakeHeldQuota(0));

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(OpenParkingSessionErrors.SessionAlreadyOpen, result.Error);
        // Disposing without commit puts the slot back to Available.
        Assert.False(unitOfWork.Committed);
        Assert.True(unitOfWork.Disposed);
    }

    [Fact]
    public async Task Motorbike_does_not_ask_booking_for_held_reservations()
    {
        var heldQuota = new FakeHeldQuota(null);
        var slots = new FakeSlotDesignator();
        var handler = NewHandler(new FakeParkingSessionRepository(), new FakeMotorbikeZones(), new FakeUnitOfWork(), slots, heldQuota);

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "59X1-234.56", "Motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, heldQuota.Calls);
        Assert.False(slots.Locked);
        Assert.Null(result.Value!.SlotCode);
    }

    private static OpenParkingSessionCommandHandler NewHandler(
        FakeParkingSessionRepository repository,
        FakeMotorbikeZones zones,
        FakeUnitOfWork unitOfWork,
        FakeSlotDesignator? slots = null,
        FakeHeldQuota? heldQuota = null) =>
        new(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock(), zones, unitOfWork,
            slots ?? new FakeSlotDesignator(), heldQuota ?? new FakeHeldQuota(0), new FakeZoneLabels());

    private sealed class FakeParkingSessionRepository : IParkingSessionRepository
    {
        public bool HasOpenSession { get; init; }
        public bool SaveSucceeds { get; init; } = true;
        public List<ParkingSession> Added { get; } = [];
        public List<ParkingSession> Saved { get; } = [];

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(HasOpenSession);

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Added.FirstOrDefault(session => session.Id == sessionId));

        public void Add(ParkingSession session) => Added.Add(session);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            if (SaveSucceeds)
                Saved.AddRange(Added.Except(Saved));
            return Task.FromResult(SaveSucceeds);
        }
    }

    // The real reader returns only approved lots, so "not found" covers unknown and not-approved lots.
    private sealed class FakeParkingLotReader(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Test Street", 10.77m, 106.70m, "Flat") : null);
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakePlateOwnerLookup(PlateOwner? owner) : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(owner);
    }

    private sealed class FakeMotorbikeZones(bool full = false, bool configured = true) : IMotorbikeZoneCounter
    {
        public static readonly Guid ZoneId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public int Taken { get; private set; }

        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken)
        {
            if (full || !configured)
                return Task.FromResult<MotorbikeSpaceTaken?>(null);
            Taken++;
            return Task.FromResult<MotorbikeSpaceTaken?>(new MotorbikeSpaceTaken(ZoneId, "M2", "Tầng 2", 79));
        }

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(configured ? new MotorbikeAvailability([new MotorbikeZoneAvailability(ZoneId, "M2", "Tầng 2", 80, full ? 80 : 1)]) : null);
    }

    private sealed class FakeUnitOfWork : IParkingUnitOfWork, IParkingTransaction
    {
        public bool Begun { get; private set; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }

        public Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            Begun = true;
            return Task.FromResult<IParkingTransaction>(this);
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeSlotDesignator(int available = 5, bool slotLeft = true) : ISlotDesignator
    {
        public static readonly Guid CarZoneId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        public bool Locked { get; private set; }
        public Guid? DesignatedFor { get; private set; }

        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken)
        {
            Locked = true;
            return Task.FromResult(available);
        }

        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken)
        {
            if (!slotLeft)
                return Task.FromResult<DesignatedSlot?>(null);
            DesignatedFor = sessionId;
            return Task.FromResult<DesignatedSlot?>(new DesignatedSlot(Guid.NewGuid(), CarZoneId, "A-01"));
        }

        public Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHeldQuota(int? held) : IHeldQuotaProvider
    {
        public int Calls { get; private set; }

        public Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(held);
        }
    }

    private sealed class FakeZoneLabels : IZoneLabelReader
    {
        public Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ZoneLabel>>(
                zoneIds.Where(id => id == FakeSlotDesignator.CarZoneId).ToDictionary(id => id, _ => new ZoneLabel("CAR-A", "Tầng 1")));
    }
}
