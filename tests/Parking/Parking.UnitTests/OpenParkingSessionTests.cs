using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Identity;
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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock());

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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(owner), new FixedClock());

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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock());

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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock());

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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: false), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock());

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
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: false), new FakePlateOwnerLookup(null), new FixedClock());

        var result = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Handler_issues_a_different_qr_token_without_plate_for_each_session()
    {
        var repository = new FakeParkingSessionRepository();
        var handler = new OpenParkingSessionCommandHandler(repository, new FakeParkingLotReader(approved: true), new FakeLotAccess(allowed: true), new FakePlateOwnerLookup(null), new FixedClock());

        var first = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-111.11", "Car", StaffId), TestContext.Current.CancellationToken);
        var second = await handler.Handle(new OpenParkingSessionCommand(LotId, "51A-222.22", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Value!.QrToken, second.Value!.QrToken);
        Assert.DoesNotContain("51A", first.Value.QrToken, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeParkingSessionRepository : IParkingSessionRepository
    {
        public bool HasOpenSession { get; init; }
        public bool SaveSucceeds { get; init; } = true;
        public List<ParkingSession> Added { get; } = [];
        public List<ParkingSession> Saved { get; } = [];

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(HasOpenSession);

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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
