using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Booking.UnitTests.Reservations;

public sealed class CheckInReservationCommandHandlerTests
{
    private static readonly Guid LotId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VehicleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly string PlateNumber = "51A-123.45";

    private readonly FakeReservationRepository _repository = new();

    [Fact]
    public async Task Handle_WhenNoReservationFound_ReturnsHasReservationFalse()
    {
        var handler = new CheckInReservationCommandHandler(_repository);
        var command = new CheckInReservationCommand(LotId, PlateNumber, DateTimeOffset.UtcNow, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasReservation);
        Assert.Null(result.Value.ReservationId);
    }

    [Fact]
    public async Task Handle_WhenActiveConfirmedReservationFound_TransitionsToUsedAndReturnsDetails()
    {
        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        _repository.Add(reservation);

        var handler = new CheckInReservationCommandHandler(_repository);
        var sessionId = Guid.NewGuid();
        var command = new CheckInReservationCommand(LotId, PlateNumber, apptTime, sessionId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasReservation);
        Assert.Equal(reservation.Id, result.Value.ReservationId);
        Assert.Equal(UserId, result.Value.UserId);
        Assert.Equal(VehicleId, result.Value.VehicleId);
        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(sessionId, reservation.SessionId);
    }

    [Fact]
    public async Task Handle_WhenArrivalIsTooEarly_ReturnsHasReservationFalse()
    {
        // WindowStartAt = appointmentTime - 60 min. Early arrival > 15 min before window = 76 min early.
        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        _repository.Add(reservation);

        var handler = new CheckInReservationCommandHandler(_repository);
        var tooEarly = apptTime.AddMinutes(-76);
        var command = new CheckInReservationCommand(LotId, PlateNumber, tooEarly, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasReservation);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public async Task Handle_WhenArrivalIsTooLate_ReturnsHasReservationFalse()
    {
        // NoShowGracePeriodMinutes = 15 min. Arrival 16 min after apptTime is too late.
        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        _repository.Add(reservation);

        var handler = new CheckInReservationCommandHandler(_repository);
        var tooLate = apptTime.AddMinutes(16);
        var command = new CheckInReservationCommand(LotId, PlateNumber, tooLate, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasReservation);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Validator_RejectsEmptyLotIdOrPlateNumber()
    {
        var validator = new CheckInReservationCommandValidator();

        var resEmptyLot = validator.Validate(new CheckInReservationCommand(Guid.Empty, PlateNumber, DateTimeOffset.UtcNow, null));
        Assert.False(resEmptyLot.IsValid);

        var resEmptyPlate = validator.Validate(new CheckInReservationCommand(LotId, "", DateTimeOffset.UtcNow, null));
        Assert.False(resEmptyPlate.IsValid);
    }

    [Theory]
    [InlineData("51A-123.45")]
    [InlineData("51a 123.45")]
    [InlineData("51A12345")]
    [Trait("Requirement", "N1")]
    public async Task Handle_WhenPlateIsCanonicalInDb_AndCheckInPlateIsFormatted_FindsAndUsesReservation(string checkInPlate)
    {
        var apptTime = DateTimeOffset.UtcNow;
        // In DB, plate is stored canonically as "51A12345"
        var reservation = CreateConfirmedReservationWithPlate(apptTime, "51A12345");
        _repository.Add(reservation);

        var handler = new CheckInReservationCommandHandler(_repository);
        var sessionId = Guid.NewGuid();
        var command = new CheckInReservationCommand(LotId, checkInPlate, apptTime, sessionId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasReservation);
        Assert.Equal(reservation.Id, result.Value.ReservationId);
        Assert.Equal(ReservationStatus.Used, reservation.Status);
    }

    private static Reservation CreateConfirmedReservation(DateTimeOffset apptTime) =>
        CreateConfirmedReservationWithPlate(apptTime, PlateNumber);

    private static Reservation CreateConfirmedReservationWithPlate(DateTimeOffset apptTime, string plateNumber)
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            UserId,
            VehicleId,
            plateNumber,
            LotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            apptTime,
            prepaidAmount: 50000,
            createdAt: apptTime.AddHours(-2));

        res.Confirm(apptTime.AddHours(-2));
        return res;
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);

        private readonly List<Reservation> _list = [];

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => !status.HasValue || r.Status == status.Value).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId &&
                (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
            Guid lotId,
            string plateNumber,
            DateTimeOffset arrivalTime,
            CancellationToken cancellationToken)
        {
            var normalized = LicensePlateNormalizer.Normalize(plateNumber);
            var res = _list.FirstOrDefault(r =>
                r.LotId == lotId &&
                LicensePlateNormalizer.Normalize(r.PlateNumber) == normalized &&
                r.Status == ReservationStatus.Confirmed &&
                arrivalTime >= r.WindowStartAt.AddMinutes(-15) &&
                arrivalTime <= r.AppointmentTime.AddMinutes(Reservation.NoShowGracePeriodMinutes));
            return Task.FromResult(res);
        }

        public void Add(Reservation reservation) => _list.Add(reservation);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
