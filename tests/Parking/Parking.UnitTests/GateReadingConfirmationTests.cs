using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;
using ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-002: staff confirm or correct a reading the system was not sure about (BR-05.a): the LowConfidencePlate incident
// is resolved, with an audit entry.
public sealed class GateReadingConfirmationTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = ReadAt.AddMinutes(1);
    private readonly ConfirmGateReadingCommandValidator validator = new();

    [Fact]
    public async Task Staff_confirm_a_pending_reading_with_an_audit_entry_and_no_plate_in_it()
    {
        var incident = Pending("51A-123.45", confidence: 0.7m);
        var incidents = new FakeIncidents(incident);
        var audit = new FakeAuditLog();
        var handler = NewHandler(incidents, audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(incident.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(incident.Id, result.Value!.IncidentId);
        Assert.Equal("StaffConfirmed", result.Value.Decision);
        Assert.True(result.Value.PlateFormatValid);
        Assert.Equal(Now, result.Value.ConfirmedAt);
        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(StaffId, incident.ResolvedBy);
        Assert.Equal(Now, incident.ResolvedAt);
        Assert.True(incidents.Saved);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.GateReadingConfirmed, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(nameof(Incident), entry.TargetType);
        Assert.Equal(incident.Id.ToString(), entry.TargetId);
        Assert.Equal("False", entry.Details!["plateCorrected"]);
        // NFR-15: no plate number in the audit details.
        Assert.DoesNotContain(entry.Details.Values, value => value.Contains("51A", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Staff_correct_the_plate_and_vehicle_type_they_see()
    {
        var incident = Pending("51A-123.4S", confidence: 0.95m); // misread "5" as "S": not a valid plate
        var audit = new FakeAuditLog();
        var handler = NewHandler(new FakeIncidents(incident), audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(incident.Id, " 51a-123.45 ", "motorbike", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal("51A-123.45", result.Value!.PlateNumber);
        Assert.Equal("Motorbike", result.Value.VehicleType);
        Assert.Equal("51A-123.4S", incident.ObservedPlate); // what the device read is kept
        Assert.Equal(VehicleType.Car, incident.ObservedVehicleType);
        // Lead review of #151: the confirmed values have their own columns, not a sentence in the resolution reason.
        Assert.Equal("51A-123.45", incident.ConfirmedPlate);
        Assert.Equal(VehicleType.Motorbike, incident.ConfirmedVehicleType);
        Assert.Equal(Incident.PlateConfirmedReason, incident.ResolutionReason);
        Assert.DoesNotContain("51A", incident.ResolutionReason!);
        var details = Assert.Single(audit.Entries).Details!;
        Assert.Equal("True", details["plateCorrected"]);
        Assert.Equal("True", details["vehicleTypeCorrected"]);
    }

    [Fact]
    public async Task A_corrected_plate_outside_the_vietnamese_format_is_accepted_and_flagged()
    {
        var incident = Pending("", confidence: 0m);
        var audit = new FakeAuditLog();
        var handler = NewHandler(new FakeIncidents(incident), audit);

        var result = await handler.Handle(new ConfirmGateReadingCommand(incident.Id, "NG-123-45", "Car", StaffId), TestContext.Current.CancellationToken);

        // Decision 2026-10-29: staff saw the vehicle; special series are not in the format rule yet.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.PlateFormatValid);
        var details = Assert.Single(audit.Entries).Details!;
        Assert.Equal("False", details["plateFormatValid"]);
        Assert.Equal("True", details["plateCorrected"]); // nothing was read, so the plate counts as corrected
    }

    [Fact]
    public async Task A_resolved_incident_cannot_be_confirmed_again()
    {
        var incident = Pending("51A-123.45", confidence: 0.5m);
        Assert.True(incident.Resolve(StaffId, Now, "first"));
        var incidents = new FakeIncidents(incident);
        var audit = new FakeAuditLog();

        var result = await NewHandler(incidents, audit).Handle(new ConfirmGateReadingCommand(incident.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.NotPending, result.Error);
        Assert.Empty(audit.Entries);
        Assert.False(incidents.Saved);
    }

    [Fact]
    public async Task The_second_of_two_staff_confirming_at_once_gets_not_pending()
    {
        var incident = Pending("51A-123.45", confidence: 0.5m);
        var incidents = new FakeIncidents(incident) { SaveSucceeds = false };

        var result = await NewHandler(incidents, new FakeAuditLog()).Handle(new ConfirmGateReadingCommand(incident.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.NotPending, result.Error);
    }

    [Fact]
    public async Task Unknown_incident_is_not_found()
    {
        var result = await NewHandler(new FakeIncidents(null), new FakeAuditLog())
            .Handle(new ConfirmGateReadingCommand(Guid.NewGuid(), "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmGateReadingErrors.ReadingNotFound, result.Error);
    }

    [Fact]
    public async Task Staff_not_assigned_to_the_incident_lot_are_refused()
    {
        var incident = Pending("51A-123.45", confidence: 0.5m);
        var handler = new ConfirmGateReadingCommandHandler(new FakeIncidents(incident), new FakeLotAccess(allowed: false), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new ConfirmGateReadingCommand(incident.Id, "51A-123.45", "Car", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Equal(IncidentStatus.Open, incident.Status);
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
        var noPlate = Pending("", confidence: 0m);
        var incidents = new FakeIncidents(null) { OpenList = [lowConfidence, badFormat, noPlate] };
        var handler = new ListPendingGateReadingsQueryHandler(incidents, new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new ListPendingGateReadingsQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(IncidentType.LowConfidencePlate, incidents.ListedType);
        Assert.Collection(result.Value!,
            first =>
            {
                Assert.Equal(lowConfidence.Id, first.IncidentId);
                Assert.Equal("51A-123.45", first.ObservedPlate);
                Assert.Equal(0.6m, first.Confidence);
                Assert.Equal("Car", first.VehicleType);
                Assert.True(first.PlateFormatValid);
                Assert.Equal(ReadAt, first.OccurredAt);
            },
            second =>
            {
                Assert.Equal(badFormat.Id, second.IncidentId);
                Assert.False(second.PlateFormatValid);
            },
            third =>
            {
                Assert.Null(third.ObservedPlate);
                Assert.False(third.PlateFormatValid);
            });
    }

    [Fact]
    public async Task Pending_list_is_refused_to_staff_not_assigned_to_the_lot()
    {
        var handler = new ListPendingGateReadingsQueryHandler(new FakeIncidents(null), new FakeLotAccess(allowed: false));

        var result = await handler.Handle(new ListPendingGateReadingsQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public void Domain_requires_a_reason_and_truncates_it_to_the_erd_limit()
    {
        var incident = Pending("51A-123.45", confidence: 0.5m);

        Assert.Throws<ArgumentException>(() => incident.Resolve(StaffId, Now, " "));
        Assert.True(incident.Resolve(StaffId, Now, new string('x', Incident.ResolutionReasonMaxLength + 10)));
        Assert.Equal(Incident.ResolutionReasonMaxLength, incident.ResolutionReason!.Length);
    }

    private static Incident Pending(string plate, decimal confidence) =>
        Incident.OpenLowConfidencePlate(Guid.NewGuid(), LotId, plate, confidence, VehicleType.Car, ReadAt);

    private static ConfirmGateReadingCommandHandler NewHandler(FakeIncidents incidents, FakeAuditLog audit) =>
        new(incidents, new FakeLotAccess(allowed: true), audit, new FixedClock());

    private sealed class FakeIncidents(Incident? incident) : IIncidentRepository
    {
        public bool SaveSucceeds { get; init; } = true;
        public bool Saved { get; private set; }
        public IReadOnlyList<Incident> OpenList { get; init; } = [];
        public IncidentType? ListedType { get; private set; }

        public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult(incident?.Id == incidentId ? incident : null);

        public Task<Incident?> FindByIdForUpdateAsync(Guid incidentId, Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken)
        {
            ListedType = incidentType;
            return Task.FromResult(OpenList);
        }

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = SaveSucceeds;
            return Task.FromResult(SaveSucceeds);
        }

        public void Add(Incident added) => throw new NotSupportedException();

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
