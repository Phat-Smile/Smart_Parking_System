using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-001 (BR-05.a): confidence from 0.9 and a valid Vietnamese plate are recognised automatically; anything else
// opens a LowConfidencePlate incident for staff (US-PARK-002).
public sealed class GateReadingTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private readonly RecordGateReadingCommandValidator validator = new();

    [Theory]
    [InlineData("51A-123.45", 0.9, GateReadingDecision.AutoAccepted)] // exactly 0.9 is enough ("từ 0.9")
    [InlineData("51A-123.45", 1.0, GateReadingDecision.AutoAccepted)]
    [InlineData("59X1-234.56", 0.95, GateReadingDecision.AutoAccepted)] // motorbike series with a second character
    [InlineData("51a 12345", 0.97, GateReadingDecision.AutoAccepted)] // spaces and lower case are normalized for the check
    [InlineData("51A-123.45", 0.899, GateReadingDecision.PendingStaff)] // just below the threshold
    [InlineData("51A-123.45", 0.0, GateReadingDecision.PendingStaff)]
    [InlineData("ABC-123", 0.99, GateReadingDecision.PendingStaff)] // confident but not a Vietnamese plate
    [InlineData("NG-123-45", 0.99, GateReadingDecision.PendingStaff)] // special series not covered yet: staff decide
    [InlineData("", 0.99, GateReadingDecision.PendingStaff)] // no plate read
    public async Task Decision_follows_br_05a(string plate, double confidence, GateReadingDecision expected)
    {
        var incidents = new FakeIncidents();
        var handler = NewHandler(incidents);

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, plate, "Car", (decimal)confidence, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(expected.ToString(), result.Value!.Decision);
        // Only a reading staff must confirm is stored, as an incident.
        Assert.Equal(expected == GateReadingDecision.PendingStaff ? 1 : 0, incidents.Saved.Count);
    }

    [Fact]
    public async Task An_auto_accepted_reading_opens_no_incident()
    {
        var incidents = new FakeIncidents();

        var result = await NewHandler(incidents).Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "car", 0.93m, StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.IncidentId);
        Assert.Equal("AutoAccepted", result.Value.Decision);
        Assert.Equal("Car", result.Value.VehicleType);
        Assert.Equal(0.93m, result.Value.Confidence);
        Assert.Equal(Now, result.Value.ReceivedAt);
        Assert.Empty(incidents.Saved);
    }

    [Fact]
    public async Task A_pending_reading_opens_a_low_confidence_plate_incident_as_in_the_erd()
    {
        var incidents = new FakeIncidents();

        var result = await NewHandler(incidents).Handle(new RecordGateReadingCommand(LotId, "  51a-123.45 ", "Motorbike", 0.899m, StaffId), TestContext.Current.CancellationToken);

        var incident = Assert.Single(incidents.Saved);
        Assert.Equal(incident.Id, result.Value!.IncidentId);
        Assert.Equal("PendingStaff", result.Value.Decision);
        Assert.Equal(IncidentType.LowConfidencePlate, incident.IncidentType);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(LotId, incident.LotId);
        // Same normalization as parking sessions, so staff open the session with this exact plate.
        Assert.Equal(ParkingSession.NormalizePlate("51a-123.45"), incident.ObservedPlate);
        Assert.Equal(VehicleType.Motorbike, incident.ObservedVehicleType);
        Assert.Equal(Now, incident.OccurredAt);
        // ERD plate_confidence numeric(3,2): rounded down so it never shows 0.90 for a value below the threshold.
        Assert.Equal(0.89m, incident.PlateConfidence);
        // The response keeps the confidence as sent.
        Assert.Equal(0.899m, result.Value.Confidence);
    }

    [Fact]
    public async Task No_plate_read_is_stored_as_a_null_observed_plate()
    {
        var incidents = new FakeIncidents();

        await NewHandler(incidents).Handle(new RecordGateReadingCommand(LotId, null, "Car", 0m, StaffId), TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(incidents.Saved).ObservedPlate);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Confidence_outside_0_to_1_is_rejected_by_the_domain(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Incident.OpenLowConfidencePlate(Guid.NewGuid(), LotId, "51A-123.45", (decimal)confidence, VehicleType.Car, Now));
    }

    [Fact]
    public async Task Handler_refuses_an_unknown_or_unapproved_lot()
    {
        var incidents = new FakeIncidents();
        var handler = new RecordGateReadingCommandHandler(incidents, new FakeLots(approved: false), new FakeLotAccess(allowed: true), new FixedClock());

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", 0.5m, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(RecordGateReadingErrors.LotNotFound, result.Error);
        Assert.Empty(incidents.Saved);
    }

    [Fact]
    public async Task Handler_refuses_staff_not_assigned_to_the_lot()
    {
        var incidents = new FakeIncidents();
        var handler = new RecordGateReadingCommandHandler(incidents, new FakeLots(approved: true), new FakeLotAccess(allowed: false), new FixedClock());

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", 0.5m, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Empty(incidents.Saved);
    }

    [Theory]
    [InlineData("51A-123.45", "Car", 0.5)]
    [InlineData(null, "Motorbike", 0.0)] // no plate read is allowed
    [InlineData("", "car", 1.0)]
    public void Validator_accepts_a_device_reading(string? plate, string vehicleType, double confidence)
    {
        Assert.True(validator.Validate(new RecordGateReadingCommand(LotId, plate, vehicleType, (decimal)confidence, StaffId)).IsValid);
    }

    [Fact]
    public void Validator_rejects_missing_or_out_of_range_confidence()
    {
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", null, StaffId)).IsValid);
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", 1.2m, StaffId)).IsValid);
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", -0.1m, StaffId)).IsValid);
    }

    [Theory]
    [InlineData("Truck")]
    [InlineData("1")]
    [InlineData("")]
    public void Validator_rejects_an_unknown_vehicle_type(string vehicleType)
    {
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, "51A-123.45", vehicleType, 0.95m, StaffId)).IsValid);
    }

    [Fact]
    public void Validator_rejects_a_missing_lot_or_a_plate_longer_than_the_erd_limit()
    {
        Assert.False(validator.Validate(new RecordGateReadingCommand(Guid.Empty, "51A-123.45", "Car", 0.95m, StaffId)).IsValid);
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, new string('1', ParkingSession.PlateNumberMaxLength + 1), "Car", 0.95m, StaffId)).IsValid);
    }

    private static RecordGateReadingCommandHandler NewHandler(FakeIncidents incidents) =>
        new(incidents, new FakeLots(approved: true), new FakeLotAccess(allowed: true), new FixedClock());

    private sealed class FakeIncidents : IIncidentRepository
    {
        private readonly List<Incident> added = [];
        public List<Incident> Saved { get; } = [];

        public void Add(Incident incident) => added.Add(incident);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved.AddRange(added);
            added.Clear();
            return Task.CompletedTask;
        }

        public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeLots(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Test Street", 10.77m, 106.70m, "Outdoor") : null);
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
