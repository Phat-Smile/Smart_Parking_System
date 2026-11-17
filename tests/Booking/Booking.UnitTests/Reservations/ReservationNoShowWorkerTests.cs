using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Workers;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationNoShowWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private readonly RecordingOutbox _outbox = new();
    private readonly FakeReservationRepository _repository;
    private readonly FakeClock _clock = new(Now);

    public ReservationNoShowWorkerTests()
    {
        _repository = new FakeReservationRepository(_outbox);
    }

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
        decimal prepaidAmount = 40000m,
        ReservationType reservationType = ReservationType.Scheduled)
    {
        var res = Reservation.CreateTemporaryHold(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            reservationType,
            appointmentTime,
            prepaidAmount,
            appointmentTime.AddHours(-2),
            durationMinutes: 120,
            tempHoldExpiresAt: appointmentTime.AddHours(-1));

        res.Confirm(appointmentTime.AddHours(-1));
        return res;
    }

    // -------------------------------------------------------------------------
    // Existing Tests (preserved)
    // -------------------------------------------------------------------------
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
        Assert.Equal(50000m, cancelledEvent.ForfeitedAmount);
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
        // Appointment was at 09:44:30 (15 minutes 30 seconds ago). 15m grace has passed, but 1m technical tolerance (total 16m) has not!
        var resId = Guid.NewGuid();
        var res = CreateConfirmedReservation(resId, Now.AddMinutes(-15).AddSeconds(-30), prepaidAmount: 50000m);
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
    // US-RES-008 Required Unit Tests (N-01 to N-12)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task N_01_HWD_Confirmed_Now_Is_AppointmentPlus15M59S_NotCancelled()
    {
        // Appointment at A, now = A + 15:59 (1 second before 16:00 cutoff)
        var appointmentTime = Now.AddMinutes(-15).AddSeconds(-59);
        var res = CreateConfirmedReservation(
            Guid.NewGuid(),
            appointmentTime,
            prepaidAmount: 20000m,
            reservationType: ReservationType.HoldWhileDriving);
        _repository.Add(res);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Equal(ReservationStatus.Confirmed, res.Status);
        Assert.False(res.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task N_02_HWD_Confirmed_Now_Is_AppointmentPlus16M00S_CancelledNoShow_Forfeited()
    {
        // Appointment at A, now = A + 16:00 exact cutoff
        var appointmentTime = Now.AddMinutes(-16);
        var res = CreateConfirmedReservation(
            Guid.NewGuid(),
            appointmentTime,
            prepaidAmount: 20000m,
            reservationType: ReservationType.HoldWhileDriving);
        _repository.Add(res);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoShow, res.CancelReason);
        Assert.True(res.IsNoShow);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);

        var @event = Assert.Single(_outbox.Events);
        var cancelled = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(res.Id, cancelled.ReservationId);
        Assert.Equal(CancelReason.NoShow.ToString(), cancelled.CancelReason);
        Assert.Equal(0m, cancelled.RefundAmount);
        Assert.Equal(20000m, cancelled.ForfeitedAmount);
    }

    [Fact]
    public async Task N_03_Scheduled_Confirmed_Prepaid60k_Now_Is_AppointmentPlus16M00S_CancelledNoShow_Forfeited()
    {
        // Scheduled Confirmed with PrepaidAmount 60,000, now = A + 16:00
        var appointmentTime = Now.AddMinutes(-16);
        var res = CreateConfirmedReservation(
            Guid.NewGuid(),
            appointmentTime,
            prepaidAmount: 60000m,
            reservationType: ReservationType.Scheduled);
        _repository.Add(res);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoShow, res.CancelReason);
        Assert.True(res.IsNoShow);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);

        var @event = Assert.Single(_outbox.Events);
        var cancelled = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(res.Id, cancelled.ReservationId);
        Assert.Equal(CancelReason.NoShow.ToString(), cancelled.CancelReason);
        Assert.Equal(0m, cancelled.RefundAmount);
        Assert.Equal(60000m, cancelled.ForfeitedAmount);
    }

    [Fact]
    public async Task N_04_PrepaidAmount_Zero_CancelledNoShow_OutcomeNull_IsNoShowTrue()
    {
        var appointmentTime = Now.AddMinutes(-20);
        var res = CreateConfirmedReservation(
            Guid.NewGuid(),
            appointmentTime,
            prepaidAmount: 0m,
            reservationType: ReservationType.Scheduled);
        _repository.Add(res);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoShow, res.CancelReason);
        Assert.True(res.IsNoShow);
        Assert.Null(res.PrepaidOutcome);

        var @event = Assert.Single(_outbox.Events);
        var cancelled = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(0m, cancelled.RefundAmount);
        Assert.Equal(0m, cancelled.ForfeitedAmount);
    }

    [Fact]
    public async Task N_05_TemporaryHold_Used_Expired_And_Cancelled_PastGracePeriod_Untouched()
    {
        var appointmentTime = Now.AddMinutes(-30);

        // 1. TemporaryHold
        var resHold = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A11111", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, appointmentTime, 50000m, appointmentTime.AddHours(-2));
        _repository.Add(resHold);

        // 2. Used
        var resUsed = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime);
        resUsed.Use(appointmentTime.AddMinutes(-5), Guid.NewGuid());
        _repository.Add(resUsed);

        // 3. Expired
        var resExpired = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A22222", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, appointmentTime, 50000m, appointmentTime.AddHours(-2));
        resExpired.Expire();
        _repository.Add(resExpired);

        // 4. Cancelled
        var resCancelled = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime);
        resCancelled.Cancel(Now.AddMinutes(-25), CancelReason.CustomerCancelled, PrepaidOutcome.Forfeited);
        _repository.Add(resCancelled);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Equal(ReservationStatus.TemporaryHold, resHold.Status);
        Assert.Equal(ReservationStatus.Used, resUsed.Status);
        Assert.Equal(ReservationStatus.Expired, resExpired.Status);
        Assert.Equal(ReservationStatus.Cancelled, resCancelled.Status);
        Assert.False(resCancelled.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task N_06_CustomerCancelled_Reservation_IsNoShowStaysFalse_WorkerUntouched()
    {
        var appointmentTime = Now.AddMinutes(-40);

        var resRefunded = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime);
        resRefunded.Cancel(Now.AddMinutes(-35), CancelReason.CustomerCancelled, PrepaidOutcome.Refunded);
        _repository.Add(resRefunded);

        var resForfeited = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime);
        resForfeited.Cancel(Now.AddMinutes(-35), CancelReason.CustomerCancelled, PrepaidOutcome.Forfeited);
        _repository.Add(resForfeited);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.False(resRefunded.IsNoShow);
        Assert.False(resForfeited.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task N_07_NoSuitableSlotCancelled_Reservation_Untouched_IsNoShowFalse()
    {
        var appointmentTime = Now.AddMinutes(-30);
        var res = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime);
        res.Cancel(Now.AddMinutes(-20), CancelReason.NoSuitableSlot, PrepaidOutcome.Refunded);
        _repository.Add(res);

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Equal(CancelReason.NoSuitableSlot, res.CancelReason);
        Assert.False(res.IsNoShow);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task N_08_SecondWorkerRun_ZeroProcessed_StillExactlyOneEventTotal()
    {
        var appointmentTime = Now.AddMinutes(-20);
        var res = CreateConfirmedReservation(Guid.NewGuid(), appointmentTime, prepaidAmount: 50000m);
        _repository.Add(res);

        var worker = CreateWorker();

        // Run 1
        var processed1 = await worker.ProcessNoShowsAsync(CancellationToken.None);
        Assert.Equal(1, processed1);
        Assert.Single(_outbox.Events);

        // Run 2
        var processed2 = await worker.ProcessNoShowsAsync(CancellationToken.None);
        Assert.Equal(0, processed2);
        Assert.Single(_outbox.Events);
    }

    [Fact]
    public async Task N_09_OneCandidateThrows_OtherCandidatesStillProcessed()
    {
        var appt1 = Now.AddMinutes(-30);
        var appt2 = Now.AddMinutes(-25);

        var res1 = CreateConfirmedReservation(Guid.NewGuid(), appt1);
        var res2 = CreateConfirmedReservation(Guid.NewGuid(), appt2);
        _repository.Add(res1);
        _repository.Add(res2);

        _repository.OnFindByIdAsync = id =>
        {
            if (id == res1.Id)
            {
                throw new InvalidOperationException("Simulated failure loading candidate 1");
            }
            return Task.FromResult<Reservation?>(_repository.GetById(id));
        };

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(ReservationStatus.Confirmed, res1.Status);
        Assert.Equal(ReservationStatus.Cancelled, res2.Status);
        Assert.Equal(CancelReason.NoShow, res2.CancelReason);
        Assert.Single(_outbox.Events);
    }

    [Fact]
    public async Task N_10_SaveThrowsDbUpdateConcurrencyException_WarningLogged_NoEvent_NextCandidateProcessed()
    {
        var appt1 = Now.AddMinutes(-30);
        var appt2 = Now.AddMinutes(-25);

        var res1 = CreateConfirmedReservation(Guid.NewGuid(), appt1);
        var res2 = CreateConfirmedReservation(Guid.NewGuid(), appt2);
        _repository.Add(res1);
        _repository.Add(res2);

        bool firstSave = true;
        _repository.OnSaveChangesAsync = () =>
        {
            if (firstSave)
            {
                firstSave = false;
                throw new DbUpdateConcurrencyException();
            }
            return Task.CompletedTask;
        };

        var worker = CreateWorker();
        var processed = await worker.ProcessNoShowsAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.True(_repository.DiscardPendingChangesCalled);
        // Only candidate 2 was successfully saved and enqueued
        Assert.Equal(ReservationStatus.Cancelled, res2.Status);
        var @event = Assert.Single(_outbox.Events);
        var cancelled = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(res2.Id, cancelled.ReservationId);
    }

    [Fact]
    public async Task N_11_CheckInBoundary_ArrivalAt14M59S_HasReservationTrue_Used_ArrivalAt15M00S_HasReservationFalse()
    {
        var apptTime = Now;
        var lotId = Guid.NewGuid();
        var plate1 = "51A11111";
        var plate2 = "51A22222";

        var res1 = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), plate1, lotId,
            VehicleGroup.Car, ReservationType.Scheduled, apptTime, 50000m, apptTime.AddHours(-1));
        res1.Confirm(apptTime.AddHours(-1));
        _repository.Add(res1);

        var res2 = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), plate2, lotId,
            VehicleGroup.Car, ReservationType.Scheduled, apptTime, 50000m, apptTime.AddHours(-1));
        res2.Confirm(apptTime.AddHours(-1));
        _repository.Add(res2);

        var handler = new CheckInReservationCommandHandler(_repository);

        // Case 1: Arrival at A + 15:59 -> Eligible, HasReservation: true, Status: Used
        var arrivalBeforeCutoff = apptTime.AddMinutes(15).AddSeconds(59);
        var result1 = await handler.Handle(
            new CheckInReservationCommand(lotId, plate1, arrivalBeforeCutoff, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result1.IsSuccess);
        Assert.True(result1.Value!.HasReservation);
        Assert.Equal(ReservationStatus.Used, res1.Status);

        // Case 2: Arrival at A + 16:00 -> Ineligible, HasReservation: false, Status remains Confirmed
        var arrivalAtCutoff = apptTime.AddMinutes(16);
        var result2 = await handler.Handle(
            new CheckInReservationCommand(lotId, plate2, arrivalAtCutoff, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result2.IsSuccess);
        Assert.False(result2.Value!.HasReservation);
        Assert.Equal(ReservationStatus.Confirmed, res2.Status);
    }

    [Fact]
    public async Task N_12_ForfeitedAmount_NoShow_And_LateCustomerCancel_And_DeserializationWithoutForfeitedAmount()
    {
        // 1. NoShow sets ForfeitedAmount = candidate.PrepaidAmount
        var resId1 = Guid.NewGuid();
        var resNoShow = CreateConfirmedReservation(resId1, Now.AddMinutes(-30), prepaidAmount: 50000m);
        _repository.Add(resNoShow);

        var worker = CreateWorker();
        await worker.ProcessNoShowsAsync(CancellationToken.None);

        var noShowEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(_outbox.Events.Last());
        Assert.Equal(50000m, noShowEvent.ForfeitedAmount);
        Assert.Equal(0m, noShowEvent.RefundAmount);

        // 2. Late customer cancel sets ForfeitedAmount = PrepaidAmount, RefundAmount = 0
        var userId = Guid.NewGuid();
        var resId2 = Guid.NewGuid();
        var lateCancelRes = CreateConfirmedReservation(resId2, Now.AddMinutes(10), prepaidAmount: 40000m); // 10m before appt (< 30m) -> forfeited
        _repository.Add(lateCancelRes);

        var cancelHandler = new CancelReservationCommandHandler(
            _repository,
            new FakePaymentClient(),
            _clock,
            NullLogger<CancelReservationCommandHandler>.Instance,
            _outbox);

        var lateResult = await cancelHandler.Handle(new CancelReservationCommand(resId2, lateCancelRes.UserId, "Late cancel"), CancellationToken.None);
        Assert.True(lateResult.IsSuccess);

        var lateCancelEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(_outbox.Events.Last());
        Assert.Equal(40000m, lateCancelEvent.ForfeitedAmount);
        Assert.Equal(0m, lateCancelEvent.RefundAmount);

        // 3. Early customer cancel sets ForfeitedAmount = 0, RefundAmount = PrepaidAmount
        var resId3 = Guid.NewGuid();
        var earlyCancelRes = CreateConfirmedReservation(resId3, Now.AddHours(2), prepaidAmount: 60000m); // 2 hours before appt -> refunded
        _repository.Add(earlyCancelRes);

        var earlyResult = await cancelHandler.Handle(new CancelReservationCommand(resId3, earlyCancelRes.UserId, "Early cancel"), CancellationToken.None);
        Assert.True(earlyResult.IsSuccess);

        var earlyCancelEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(_outbox.Events.Last());
        Assert.Equal(0m, earlyCancelEvent.ForfeitedAmount);
        Assert.Equal(60000m, earlyCancelEvent.RefundAmount);

        // 4. Backward-compatible JSON deserialization without ForfeitedAmount property
        var jsonWithoutForfeited = $$"""
        {
            "ReservationId": "{{Guid.NewGuid()}}",
            "UserId": "{{Guid.NewGuid()}}",
            "LotId": "{{Guid.NewGuid()}}",
            "CancelReason": "NoShow",
            "RefundAmount": 0,
            "CancelledAtUtc": "2026-11-10T10:00:00Z"
        }
        """;

        var deserializedWithout = JsonSerializer.Deserialize<ReservationCancelledIntegrationEvent>(jsonWithoutForfeited);
        Assert.NotNull(deserializedWithout);
        Assert.Equal(0m, deserializedWithout.ForfeitedAmount);

        // 5. JSON deserialization with ForfeitedAmount property
        var jsonWithForfeited = $$"""
        {
            "ReservationId": "{{Guid.NewGuid()}}",
            "UserId": "{{Guid.NewGuid()}}",
            "LotId": "{{Guid.NewGuid()}}",
            "CancelReason": "NoShow",
            "RefundAmount": 0,
            "CancelledAtUtc": "2026-11-10T10:00:00Z",
            "ForfeitedAmount": 45000
        }
        """;

        var deserializedWith = JsonSerializer.Deserialize<ReservationCancelledIntegrationEvent>(jsonWithForfeited);
        Assert.NotNull(deserializedWith);
        Assert.Equal(45000m, deserializedWith.ForfeitedAmount);
    }

    // -------------------------------------------------------------------------
    // Test Doubles
    // -------------------------------------------------------------------------
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];

        public Func<Guid, Task<Reservation?>>? OnFindByIdAsync { get; set; }
        public Func<Task>? OnSaveChangesAsync { get; set; }
        public bool DiscardPendingChangesCalled { get; private set; }

        public void Add(Reservation r) => _list.Add(r);
        public Reservation? GetById(Guid id) => _list.FirstOrDefault(r => r.Id == id);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (OnFindByIdAsync != null)
            {
                return OnFindByIdAsync(id);
            }

            return Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
        }

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default)
        {
            var normalizedPlate = LicensePlateNormalizer.Normalize(plateNumber);
            var utcArrival = arrivalTime.ToUniversalTime();

            return Task.FromResult(_list.FirstOrDefault(r =>
                r.LotId == lotId &&
                LicensePlateNormalizer.Normalize(r.PlateNumber) == normalizedPlate &&
                r.Status == ReservationStatus.Confirmed &&
                utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
                utcArrival < Reservation.GetNoShowCutoff(r.AppointmentTime)));
        }

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private readonly RecordingOutbox? _outbox;

        public FakeReservationRepository(RecordingOutbox? outbox = null)
        {
            _outbox = outbox;
        }

        public void DiscardPendingChanges()
        {
            DiscardPendingChangesCalled = true;
            _outbox?.Discard();
        }

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        {
            var graceThreshold = Reservation.GetNoShowCandidateThreshold(now);
            return Task.FromResult<IReadOnlyList<Reservation>>(
                _list.Where(r => r.Status == ReservationStatus.Confirmed && r.AppointmentTime <= graceThreshold)
                     .OrderBy(r => r.AppointmentTime)
                     .Take(batchSize)
                     .ToList());
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (OnSaveChangesAsync != null)
            {
                await OnSaveChangesAsync();
            }

            _outbox?.Commit();
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

    private sealed class FakePaymentClient : IReservationPaymentClient
    {
        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(
            Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));

        public Task<Result<ReservationDebitState>> RefundAsync(
            Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Refunded));
    }

    private sealed class DbUpdateConcurrencyException : Exception { }
}
