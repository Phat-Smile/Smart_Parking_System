using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.GateReadings;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-001 (BR-05.a): confidence from 0.9 and a valid Vietnamese plate are recognised automatically; anything else
// goes to staff (US-PARK-002).
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
    public void Decision_follows_br_05a(string plate, double confidence, GateReadingDecision expected)
    {
        var reading = GateReading.Record(Guid.NewGuid(), LotId, plate, VehicleType.Car, (decimal)confidence, Now, StaffId);

        Assert.Equal(expected, reading.Decision);
    }

    [Fact]
    public void Reading_keeps_the_raw_plate_and_the_session_form_of_the_plate()
    {
        var reading = GateReading.Record(Guid.NewGuid(), LotId, "  51a-123.45 ", VehicleType.Car, 0.95m, Now, StaffId);

        Assert.Equal("51a-123.45", reading.RawPlate);
        // Same normalization as parking sessions, so staff open the session with this exact plate.
        Assert.Equal(ParkingSession.NormalizePlate("51a-123.45"), reading.PlateNumber);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Confidence_outside_0_to_1_is_rejected_by_the_domain(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GateReading.Record(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, (decimal)confidence, Now, StaffId));
    }

    [Fact]
    public async Task Handler_records_the_reading_with_its_decision()
    {
        var readings = new FakeReadings();
        var handler = new RecordGateReadingCommandHandler(readings, new FakeLots(approved: true), new FakeLotAccess(allowed: true), new FixedClock());

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "car", 0.93m, StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(readings.Saved);
        Assert.Equal(saved.Id, result.Value!.ReadingId);
        Assert.Equal("AutoAccepted", result.Value.Decision);
        Assert.Equal("Car", result.Value.VehicleType);
        Assert.Equal(0.93m, result.Value.Confidence);
        Assert.Equal(Now, result.Value.ReceivedAt);
        Assert.Equal(StaffId, saved.RecordedBy);
    }

    [Fact]
    public async Task Handler_refuses_an_unknown_or_unapproved_lot()
    {
        var readings = new FakeReadings();
        var handler = new RecordGateReadingCommandHandler(readings, new FakeLots(approved: false), new FakeLotAccess(allowed: true), new FixedClock());

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", 0.95m, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(RecordGateReadingErrors.LotNotFound, result.Error);
        Assert.Empty(readings.Saved);
    }

    [Fact]
    public async Task Handler_refuses_staff_not_assigned_to_the_lot()
    {
        var readings = new FakeReadings();
        var handler = new RecordGateReadingCommandHandler(readings, new FakeLots(approved: true), new FakeLotAccess(allowed: false), new FixedClock());

        var result = await handler.Handle(new RecordGateReadingCommand(LotId, "51A-123.45", "Car", 0.95m, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Empty(readings.Saved);
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
    public void Validator_rejects_a_missing_lot_or_a_plate_longer_than_the_limit()
    {
        Assert.False(validator.Validate(new RecordGateReadingCommand(Guid.Empty, "51A-123.45", "Car", 0.95m, StaffId)).IsValid);
        Assert.False(validator.Validate(new RecordGateReadingCommand(LotId, new string('1', GateReading.RawPlateMaxLength + 1), "Car", 0.95m, StaffId)).IsValid);
    }

    private sealed class FakeReadings : IGateReadingRepository
    {
        private readonly List<GateReading> added = [];
        public List<GateReading> Saved { get; } = [];

        public void Add(GateReading reading) => added.Add(reading);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved.AddRange(added);
            added.Clear();
            return Task.CompletedTask;
        }
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
