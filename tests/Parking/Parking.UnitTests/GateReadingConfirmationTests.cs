using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.GateReadings;
using ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;
using ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-002: staff confirm or correct a reading the system was not sure about (BR-05.a), with an audit entry.
public sealed class GateReadingConfirmationTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DeviceStaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StaffId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = ReadAt.AddMinutes(1);
    private readonly ConfirmGateReadingCommandValidator validator = new();

    [Fact]
    public async Task Staff_confirm_a_pending_reading_with_an_audit_entry_and_no_plate_in_it()
    {
        var reading = Pending("51A-123.45", confidence: 0.7m);
        var readings = new FakeReadings(reading);
        var audit = new FakeAuditLog();
        var handler = NewHandler(readings, audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(reading.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("StaffConfirmed", result.Value!.Decision);
        Assert.True(result.Value.PlateFormatValid);
        Assert.Equal(Now, result.Value.ConfirmedAt);
        Assert.Equal(StaffId, reading.ConfirmedBy);
        Assert.True(readings.Saved);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.GateReadingConfirmed, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(reading.Id.ToString(), entry.TargetId);
        Assert.Equal("False", entry.Details!["plateCorrected"]);
        // NFR-15: no plate number in the audit details.
        Assert.DoesNotContain(entry.Details.Values, value => value.Contains("51A", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Staff_correct_the_plate_and_vehicle_type_they_see()
    {
        var reading = Pending("51A-123.4S", confidence: 0.95m); // misread "5" as "S": not a valid plate
        var audit = new FakeAuditLog();
        var handler = NewHandler(new FakeReadings(reading), audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(reading.Id, " 51a-123.45 ", "motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal("51A-123.45", result.Value!.PlateNumber);
        Assert.Equal("Motorbike", result.Value.VehicleType);
        Assert.Equal("51A-123.4S", reading.RawPlate); // what the device read is kept
        var details = Assert.Single(audit.Entries).Details!;
        Assert.Equal("True", details["plateCorrected"]);
        Assert.Equal("True", details["vehicleTypeCorrected"]);
    }

    [Fact]
    public async Task A_corrected_plate_outside_the_vietnamese_format_is_accepted_and_flagged()
    {
        var reading = Pending("", confidence: 0m);
        var audit = new FakeAuditLog();
        var handler = NewHandler(new FakeReadings(reading), audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(reading.Id, "NG-123-45", "Car", StaffId), TestContext.Current.CancellationToken);

        // Decision 2026-10-29: staff saw the vehicle; special series are not in the format rule yet.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.PlateFormatValid);
        Assert.Equal("False", Assert.Single(audit.Entries).Details!["plateFormatValid"]);
    }

    [Fact]
    public async Task An_auto_accepted_reading_cannot_be_confirmed()
    {
        var reading = GateReading.Record(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, 0.95m, ReadAt, DeviceStaffId);
        var readings = new FakeReadings(reading);
        var audit = new FakeAuditLog();

        var result = await NewHandler(readings, audit).Handle(new ConfirmGateReadingCommand(reading.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.NotPending, result.Error);
        Assert.Empty(audit.Entries);
        Assert.False(readings.Saved);
    }

    [Fact]
    public async Task The_second_of_two_staff_confirming_at_once_gets_not_pending()
    {
        var reading = Pending("51A-123.45", confidence: 0.5m);
        var readings = new FakeReadings(reading) { SaveSucceeds = false };

        var result = await NewHandler(readings, new FakeAuditLog()).Handle(new ConfirmGateReadingCommand(reading.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.NotPending, result.Error);
    }

    [Fact]
    public async Task Unknown_reading_is_not_found()
    {
        var result = await NewHandler(new FakeReadings(null), new FakeAuditLog())
            .Handle(new ConfirmGateReadingCommand(Guid.NewGuid(), "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.ReadingNotFound, result.Error);
    }

    [Fact]
    public async Task Staff_not_assigned_to_the_reading_lot_are_refused()
    {
        var reading = Pending("51A-123.45", confidence: 0.5m);
        var handler = new ConfirmGateReadingCommandHandler(new FakeReadings(reading), new FakeLotAccess(allowed: false), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new ConfirmGateReadingCommand(reading.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Equal(GateReadingDecision.PendingStaff, reading.Decision);
    }

    [Theory]
    [InlineData("", "Car")]
    [InlineData("   ", "Car")]
    [InlineData("51A-123.45", "Truck")]
    [InlineData("51A-123.45", "")]
    public void Validator_requires_a_plate_and_a_known_vehicle_type(string plate, string vehicleType)
    {
        Assert.False(validator.Validate(new ConfirmGateReadingCommand(Guid.NewGuid(), plate, vehicleType, StaffId)).IsValid);
    }

    [Fact]
    public void Validator_rejects_a_plate_longer_than_the_session_limit()
    {
        var plate = new string('1', ParkingSession.PlateNumberMaxLength + 1);

        Assert.False(validator.Validate(new ConfirmGateReadingCommand(Guid.NewGuid(), plate, "Car", StaffId)).IsValid);
    }

    [Fact]
    public async Task Pending_list_shows_why_each_reading_was_not_accepted()
    {
        var lowConfidence = Pending("51A-123.45", confidence: 0.6m);
        var badFormat = Pending("ABC-123", confidence: 0.99m);
        var handler = new ListPendingGateReadingsQueryHandler(new FakeReadings(null) { PendingList = [lowConfidence, badFormat] }, new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new ListPendingGateReadingsQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Collection(result.Value!,
            first =>
            {
                Assert.Equal(lowConfidence.Id, first.ReadingId);
                Assert.Equal(0.6m, first.Confidence);
                Assert.True(first.PlateFormatValid);
            },
            second =>
            {
                Assert.Equal(badFormat.Id, second.ReadingId);
                Assert.False(second.PlateFormatValid);
            });
    }

    [Fact]
    public async Task Pending_list_is_refused_to_staff_not_assigned_to_the_lot()
    {
        var handler = new ListPendingGateReadingsQueryHandler(new FakeReadings(null), new FakeLotAccess(allowed: false));

        var result = await handler.Handle(new ListPendingGateReadingsQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public void Domain_requires_a_plate_to_confirm()
    {
        var reading = Pending("51A-123.45", confidence: 0.5m);

        Assert.Throws<ArgumentException>(() => reading.Confirm(" ", VehicleType.Car, StaffId, Now));
    }

    private static GateReading Pending(string plate, decimal confidence)
    {
        var reading = GateReading.Record(Guid.NewGuid(), LotId, plate, VehicleType.Car, confidence, ReadAt, DeviceStaffId);
        Assert.Equal(GateReadingDecision.PendingStaff, reading.Decision);
        return reading;
    }

    private static ConfirmGateReadingCommandHandler NewHandler(FakeReadings readings, FakeAuditLog audit) =>
        new(readings, new FakeLotAccess(allowed: true), audit, new FixedClock());

    private sealed class FakeReadings(GateReading? reading) : IGateReadingRepository
    {
        public bool SaveSucceeds { get; init; } = true;
        public bool Saved { get; private set; }
        public IReadOnlyList<GateReading> PendingList { get; init; } = [];

        public Task<GateReading?> FindByIdAsync(Guid readingId, CancellationToken cancellationToken) =>
            Task.FromResult(reading?.Id == readingId ? reading : null);

        public Task<IReadOnlyList<GateReading>> ListPendingByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(PendingList);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = SaveSucceeds;
            return Task.FromResult(SaveSucceeds);
        }

        public void Add(GateReading added) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
