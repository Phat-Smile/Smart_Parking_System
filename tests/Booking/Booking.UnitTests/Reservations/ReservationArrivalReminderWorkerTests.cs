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

public sealed class ReservationArrivalReminderWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private readonly RecordingOutbox _outbox = new();
    private readonly FakeReservationRepository _repository;
    private readonly FakeClock _clock = new(Now);

    public ReservationArrivalReminderWorkerTests()
    {
        _repository = new FakeReservationRepository(_outbox);
    }

    private ReservationArrivalReminderWorker CreateWorker(int batchSize = 50)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReservationRepository>(_repository);
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IOutboxWriter>(_outbox);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new ReservationArrivalReminderOptions
        {
            IntervalSeconds = 60,
            BatchSize = batchSize
        });

        return new ReservationArrivalReminderWorker(scopeFactory, options, NullLogger<ReservationArrivalReminderWorker>.Instance);
    }

    private static Reservation CreateConfirmedReservation(
        Guid id,
        DateTimeOffset appointmentTime,
        DateTimeOffset? createdAt = null,
        string plateNumber = "51A12345")
    {
        var created = createdAt ?? appointmentTime.AddHours(-2);
        var res = Reservation.CreateTemporaryHold(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            plateNumber,
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            appointmentTime,
            40000m,
            created,
            durationMinutes: 120,
            tempHoldExpiresAt: appointmentTime.AddHours(-1));

        res.Confirm(created.AddMinutes(5));
        return res;
    }

    [Fact]
    public async Task ProcessRemindersAsync_When30MinutesBeforeArrival_DispatchesThirtyMinutesBeforeArrivalEvent_AndPersistsToEntity()
    {
        // Appointment is at 10:25. Current time is 10:00 (25m before arrival, so >= 30m mark).
        // Created at 08:25 (> 30m before arrival).
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(25), createdAt: Now.AddHours(-2), plateNumber: "51A-123.45");
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessRemindersAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        var @event = Assert.Single(_outbox.Events);
        var reminderEvent = Assert.IsType<ReservationArrivalReminderIntegrationEvent>(@event);

        Assert.Equal(res.UserId, reminderEvent.AccountId);
        Assert.Equal(res.Id, reminderEvent.ReservationId);
        Assert.Equal(res.LotId, reminderEvent.LotId);
        Assert.Equal(ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival, reminderEvent.ReminderType);
        Assert.Equal(res.AppointmentTime.UtcDateTime, reminderEvent.ScheduledArrivalTimeUtc);
        Assert.Equal(Reservation.GetNoShowCutoff(res.AppointmentTime).UtcDateTime, reminderEvent.CutoffTimeUtc);
        Assert.Equal("51A***45", reminderEvent.MaskedPlateNumber);
        Assert.DoesNotContain("123", reminderEvent.MaskedPlateNumber);

        // Verify entity database column is marked
        Assert.NotNull(res.ArrivalReminderSentAtUtc);
        Assert.Null(res.NoShowWarningSentAtUtc);
    }

    [Fact]
    public async Task ProcessRemindersAsync_WhenCreatedWithin30MinutesOfArrival_SkipsThirtyMinutesBeforeArrival()
    {
        // Appointment is at 10:20 (20m from now).
        // Reservation was created at 10:05 (only 15 minutes before arrival time).
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(20), createdAt: Now.AddMinutes(5));
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessRemindersAsync(CancellationToken.None);

        Assert.Equal(0, processedCount);
        Assert.Empty(_outbox.Events);
        Assert.Null(res.ArrivalReminderSentAtUtc);
    }

    [Fact]
    public async Task ProcessRemindersAsync_When5MinutesBeforeNoShowCutoff_DispatchesFiveMinutesBeforeNoShowEvent_AndPersistsToEntity()
    {
        // Appointment was at 09:47. Cutoff is 09:47 + 16m = 10:03.
        // 5 minutes before cutoff is 09:58. Current time is 10:00 (within [09:58, 10:03)).
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-13), createdAt: Now.AddHours(-2), plateNumber: "29A98765");
        _repository.Add(res);

        var worker = CreateWorker();
        var processedCount = await worker.ProcessRemindersAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        var @event = Assert.Single(_outbox.Events);
        var reminderEvent = Assert.IsType<ReservationArrivalReminderIntegrationEvent>(@event);

        Assert.Equal(res.UserId, reminderEvent.AccountId);
        Assert.Equal(res.Id, reminderEvent.ReservationId);
        Assert.Equal(res.LotId, reminderEvent.LotId);
        Assert.Equal(ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow, reminderEvent.ReminderType);
        Assert.Equal(res.AppointmentTime.UtcDateTime, reminderEvent.ScheduledArrivalTimeUtc);
        Assert.Equal(Reservation.GetNoShowCutoff(res.AppointmentTime).UtcDateTime, reminderEvent.CutoffTimeUtc);
        Assert.Equal("29A***65", reminderEvent.MaskedPlateNumber);

        // Verify entity database column is marked
        Assert.NotNull(res.NoShowWarningSentAtUtc);
    }

    [Fact]
    public async Task ProcessRemindersAsync_Idempotency_SecondRunDoesNotDispatchDuplicate()
    {
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(25), createdAt: Now.AddHours(-2));
        _repository.Add(res);

        var worker = CreateWorker();

        // Run 1 - dispatches and sets timestamp on entity
        var processed1 = await worker.ProcessRemindersAsync(CancellationToken.None);
        Assert.Equal(1, processed1);
        Assert.Single(_outbox.Events);
        Assert.NotNull(res.ArrivalReminderSentAtUtc);

        // Run 2 - entity already marked, candidate list skips it, 0 processed
        var processed2 = await worker.ProcessRemindersAsync(CancellationToken.None);
        Assert.Equal(0, processed2);
        Assert.Single(_outbox.Events);
    }

    [Fact]
    public async Task ProcessRemindersAsync_NonConfirmedReservations_AreIgnored()
    {
        var appt = Now.AddMinutes(25);

        // Cancelled
        var cancelled = CreateConfirmedReservation(Guid.NewGuid(), appt);
        cancelled.Cancel(Now.AddMinutes(-10), CancelReason.CustomerCancelled);
        _repository.Add(cancelled);

        // Used
        var used = CreateConfirmedReservation(Guid.NewGuid(), appt);
        used.Use(Now.AddMinutes(-5), Guid.NewGuid());
        _repository.Add(used);

        // TemporaryHold
        var hold = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A11111", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, appt, 40000m, Now.AddMinutes(-10));
        _repository.Add(hold);

        var worker = CreateWorker();
        var processed = await worker.ProcessRemindersAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task ProcessRemindersAsync_WhenOneCandidateThrows_OtherCandidatesStillProcessed()
    {
        var appt1 = Now.AddMinutes(25);
        var appt2 = Now.AddMinutes(20);

        var res1 = CreateConfirmedReservation(Guid.NewGuid(), appt1);
        var res2 = CreateConfirmedReservation(Guid.NewGuid(), appt2);
        _repository.Add(res1);
        _repository.Add(res2);

        _repository.OnFindByIdAsync = id =>
        {
            if (id == res1.Id)
                throw new InvalidOperationException("Simulated candidate 1 database error");
            return Task.FromResult<Reservation?>(_repository.GetById(id));
        };

        var worker = CreateWorker();
        var processed = await worker.ProcessRemindersAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var @event = Assert.Single(_outbox.Events);
        var reminderEvent = Assert.IsType<ReservationArrivalReminderIntegrationEvent>(@event);
        Assert.Equal(res2.Id, reminderEvent.ReservationId);
    }

    [Theory]
    [InlineData("51A-123.45", "51A***45")]
    [InlineData("51A12345", "51A***45")]
    [InlineData("1234", "****")]
    [InlineData(null, null)]
    public void MaskPlateNumber_MasksCorrectly(string? input, string? expected)
    {
        var actual = ReservationArrivalReminderWorker.MaskPlateNumber(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reservation_MarkArrivalReminderSent_ReturnsFalseWhenAlreadyMarked()
    {
        var res = CreateConfirmedReservation(Guid.NewGuid(), Now.AddMinutes(25));
        Assert.Null(res.ArrivalReminderSentAtUtc);

        var first = res.MarkArrivalReminderSent(Now.UtcDateTime);
        Assert.True(first);
        Assert.NotNull(res.ArrivalReminderSentAtUtc);

        var second = res.MarkArrivalReminderSent(Now.AddMinutes(1).UtcDateTime);
        Assert.False(second);
    }

    [Fact]
    public void Reservation_MarkNoShowWarningSent_ReturnsFalseWhenAlreadyMarked()
    {
        var res = CreateConfirmedReservation(Guid.NewGuid(), Now.AddMinutes(25));
        Assert.Null(res.NoShowWarningSentAtUtc);

        var first = res.MarkNoShowWarningSent(Now.UtcDateTime);
        Assert.True(first);
        Assert.NotNull(res.NoShowWarningSentAtUtc);

        var second = res.MarkNoShowWarningSent(Now.AddMinutes(1).UtcDateTime);
        Assert.False(second);
    }

    // -------------------------------------------------------------------------
    // Test Doubles
    // -------------------------------------------------------------------------
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        private readonly RecordingOutbox? _outbox;

        public Func<Guid, Task<Reservation?>>? OnFindByIdAsync { get; set; }

        public FakeReservationRepository(RecordingOutbox? outbox = null)
        {
            _outbox = outbox;
        }

        public void Add(Reservation r) => _list.Add(r);
        public Reservation? GetById(Guid id) => _list.FirstOrDefault(r => r.Id == id);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (OnFindByIdAsync != null)
                return OnFindByIdAsync(id);

            return Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
        }

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

        public Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void DiscardPendingChanges() => _outbox?.Discard();

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListArrivalReminderCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        {
            var minTime = now.AddMinutes(-20);
            var maxTime = now.AddMinutes(35);

            return Task.FromResult<IReadOnlyList<Reservation>>(
                _list.Where(r => r.Status == ReservationStatus.Confirmed &&
                                 r.AppointmentTime >= minTime &&
                                 r.AppointmentTime <= maxTime &&
                                 (
                                     (r.ArrivalReminderSentAtUtc == null && r.AppointmentTime <= now.AddMinutes(30)) ||
                                     (r.NoShowWarningSentAtUtc == null && r.AppointmentTime <= now.AddMinutes(-10))
                                 ))
                     .OrderBy(r => r.AppointmentTime)
                     .Take(batchSize)
                     .ToList());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _outbox?.Commit();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        private readonly List<object> _committed = [];
        private readonly List<object> _pending = [];

        public IReadOnlyList<object> Events => _committed;

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class =>
            _pending.Add(integrationEvent);

        public void Commit()
        {
            _committed.AddRange(_pending);
            _pending.Clear();
        }

        public void Discard()
        {
            _pending.Clear();
        }
    }

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }
}
