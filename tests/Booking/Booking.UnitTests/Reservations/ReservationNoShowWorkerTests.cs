using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Workers;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationNoShowWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repository = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeClock _clock = new(Now);

    private ReservationNoShowWorker CreateWorker(int batchSize = 50)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReservationRepository>(_repository);
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IOutboxWriter>(_outbox);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new ReservationNoShowOptions
        {
            IntervalSeconds = 60,
            BatchSize = batchSize
        });

        return new ReservationNoShowWorker(scopeFactory, options, NullLogger<ReservationNoShowWorker>.Instance);
    }

    private static Reservation CreateConfirmedReservation(
        Guid id,
        DateTimeOffset appointmentTime,
        decimal prepaidAmount = 40000m)
    {
        var res = Reservation.CreateTemporaryHold(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            appointmentTime,
            prepaidAmount,
            appointmentTime.AddHours(-2),
            durationMinutes: 120,
            tempHoldExpiresAt: appointmentTime.AddHours(-1));

        res.Confirm(appointmentTime.AddHours(-1));
        return res;
    }

    [Fact]
    public async Task ProcessNoShowsAsync_WhenGracePeriodElapsed_CancelsReservationAndForfeitsDeposit()
    {
        // Appointment was at 09:30. Grace period + technical tolerance is 18 minutes (09:48). Current time is 10:00.
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-30), prepaidAmount: 50000m);
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoShow, res.CancelReason);
        Assert.True(res.IsNoShow);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);
        Assert.Equal(Now, res.CancelledAt);

        // Verify outbox integration event
        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(res.Id, cancelledEvent.ReservationId);
        Assert.Equal(res.UserId, cancelledEvent.UserId);
        Assert.Equal(res.LotId, cancelledEvent.LotId);
        Assert.Equal(CancelReason.NoShow.ToString(), cancelledEvent.CancelReason);
        Assert.Equal(0m, cancelledEvent.RefundAmount);
    }

    [Fact]
    public async Task ProcessNoShowsAsync_WhenWithinGracePeriod_DoesNotCancel()
    {
        // Appointment was at 09:50. Grace period ends at 10:05. Current time is 10:00 -> not eligible yet.
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-10), prepaidAmount: 50000m);
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processedCount);
        Assert.Equal(ReservationStatus.Confirmed, res.Status);
        Assert.Null(res.CancelReason);
        Assert.False(res.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task ProcessNoShowsAsync_WhenWithinTechnicalToleranceBuffer_DoesNotCancel()
    {
        // Appointment was at 09:44 (16 minutes ago). 15m grace has passed, but 3m technical tolerance (total 18m) has not!
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-16), prepaidAmount: 50000m);
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processedCount);
        Assert.Equal(ReservationStatus.Confirmed, res.Status);
        Assert.Null(res.CancelReason);
        Assert.False(res.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task ProcessNoShowsAsync_WhenZeroDeposit_CancelsWithoutForfeiture()
    {
        // Appointment at 09:00, prepaid 0đ.
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-60), prepaidAmount: 0m);
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoShow, res.CancelReason);
        Assert.True(res.IsNoShow);
        Assert.Null(res.PrepaidOutcome);
    }

    [Fact]
    public async Task ProcessNoShowsAsync_WhenNoCandidates_ReturnsZero()
    {
        var worker = CreateWorker();
        var processedCount = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processedCount);
        Assert.Empty(_outbox.Events);
    }

    // -------------------------------------------------------------------------
    // Test Doubles
    // -------------------------------------------------------------------------
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];

        public void Add(Reservation r) => _list.Add(r);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.LotId == lotId && r.PlateNumber == plateNumber && r.Status == ReservationStatus.Confirmed));

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void DiscardPendingChanges() { }

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        {
            var graceThreshold = now.AddMinutes(-(Reservation.NoShowGracePeriodMinutes + Reservation.TechnicalToleranceMinutes));
            return Task.FromResult<IReadOnlyList<Reservation>>(
                _list.Where(r => r.Status == ReservationStatus.Confirmed && r.AppointmentTime <= graceThreshold)
                     .Take(batchSize)
                     .ToList());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class =>
            Events.Add(integrationEvent);
    }

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }
}
