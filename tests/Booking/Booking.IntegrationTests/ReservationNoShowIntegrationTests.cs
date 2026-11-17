using System.Threading;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Workers;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationNoShowIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset AppointmentTime = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ConfirmedAtTime = AppointmentTime.AddHours(-2);

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class TrackingPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];

        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(
            Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));

        public Task<Result<ReservationDebitState>> RefundAsync(
            Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            lock (RefundCalls)
            {
                RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            }
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Refunded));
        }
    }

    private ServiceProvider CreateProvider(FakeClock clock, TrackingPaymentClient? paymentClient = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IReservationPaymentClient>(paymentClient ?? new TrackingPaymentClient());
        services.AddScoped<CancelReservationCommandHandler>();
        services.AddScoped<CheckInReservationCommandHandler>();
        services.Configure<ReservationNoShowOptions>(opt =>
        {
            opt.BatchSize = 50;
            opt.IntervalSeconds = 60;
        });
        services.AddScoped<ReservationNoShowWorker>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureMigratedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // =========================================================================
    // N-13: Worker vs internal check-in race at A+14:59 on the same reservation
    // -> final state is exactly one of Used or Cancelled(NoShow).
    // If Used, there is no NoShow event in the outbox.
    // =========================================================================
    [Fact]
    public async Task N_13_Worker_Vs_CheckIn_Race_Concluded_Atomically()
    {
        var ct = TestContext.Current.CancellationToken;
        // Check-in arrives at A + 14:59 (eligible for check-in)
        var arrivalAt = AppointmentTime.AddMinutes(14).AddSeconds(59);
        // Worker clock at A + 15:00 (eligible for NoShow)
        var workerClock = new FakeClock(AppointmentTime.AddMinutes(15));

        await using var provider = CreateProvider(workerClock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed Confirmed reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        var barrier = new Barrier(2);

        async Task RunCheckInAsync()
        {
            try
            {
                await using var scope = provider.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<CheckInReservationCommandHandler>();
                barrier.SignalAndWait(ct);
                await handler.Handle(new CheckInReservationCommand(lotId, plateNumber, arrivalAt, Guid.NewGuid()), ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.InnerException?.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Concurrency loss is expected when the concurrent worker wins the race
            }
        }

        async Task RunWorkerAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var worker = scope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            barrier.SignalAndWait(ct);
            await worker.ProcessNoShowsAsync(ct);
        }

        var checkInTask = Task.Run(RunCheckInAsync, ct);
        var workerTask = Task.Run(RunWorkerAsync, ct);

        await Task.WhenAll(checkInTask, workerTask);

        // Verify database state and outbox
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var finalRes = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);

            // Exactly one of Used or Cancelled(NoShow)
            Assert.True(
                finalRes.Status == ReservationStatus.Used ^
                (finalRes.Status == ReservationStatus.Cancelled && finalRes.CancelReason == CancelReason.NoShow && finalRes.IsNoShow),
                $"Expected either Used or Cancelled(NoShow), but was {finalRes.Status} with reason {finalRes.CancelReason}");

            var outboxMessages = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == reservationId)
                .ToListAsync(ct);

            if (finalRes.Status == ReservationStatus.Used)
            {
                // If Used won: zero NoShow events in the outbox
                Assert.DoesNotContain(outboxMessages, m => m.EventType == "booking.reservation-cancelled.v1");
            }
            else
            {
                // If NoShow won: exactly one cancelled event in the outbox
                Assert.Single(outboxMessages.Where(m => m.EventType == "booking.reservation-cancelled.v1"));
            }
        }
    }

    // =========================================================================
    // N-14: Worker vs customer cancel race
    // -> exactly one of Cancelled(CustomerCancelled) or Cancelled(NoShow);
    // IsNoShow matches the winner; at most 1 cancel event.
    // =========================================================================
    [Fact]
    public async Task N_14_Worker_Vs_CustomerCancel_Race_Concluded_Atomically()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(15));

        await using var provider = CreateProvider(clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51B" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed Confirmed reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        var barrier = new Barrier(2);

        async Task RunCustomerCancelAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();
            barrier.SignalAndWait(ct);
            await handler.Handle(new CancelReservationCommand(reservationId, userId, "Customer cancelling at same time"), ct);
        }

        async Task RunWorkerAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var worker = scope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            barrier.SignalAndWait(ct);
            await worker.ProcessNoShowsAsync(ct);
        }

        var cancelTask = Task.Run(RunCustomerCancelAsync, ct);
        var workerTask = Task.Run(RunWorkerAsync, ct);

        await Task.WhenAll(cancelTask, workerTask);

        // Verify database state and outbox
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var finalRes = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);

            Assert.Equal(ReservationStatus.Cancelled, finalRes.Status);

            // Exactly one winner: CustomerCancelled or NoShow
            Assert.True(
                finalRes.CancelReason == CancelReason.CustomerCancelled ^
                finalRes.CancelReason == CancelReason.NoShow,
                $"Unexpected cancel reason: {finalRes.CancelReason}");

            // IsNoShow matches the winner
            if (finalRes.CancelReason == CancelReason.NoShow)
            {
                Assert.True(finalRes.IsNoShow);
            }
            else
            {
                Assert.False(finalRes.IsNoShow);
            }

            // At most 1 cancel event in outbox for this reservation
            var cancelEvents = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == reservationId && m.EventType == "booking.reservation-cancelled.v1")
                .ToListAsync(ct);

            Assert.Single(cancelEvents);
        }
    }

    // =========================================================================
    // N-15: After the no-show, held quota for the lot drops by 1.
    // =========================================================================
    [Fact]
    public async Task N_15_After_NoShow_HeldQuota_Drops_By_One()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(15));

        await using var provider = CreateProvider(clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51C" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed Confirmed reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // Before no-show: held quota reader sees 1 active reservation
        await using (var checkScope = provider.CreateAsyncScope())
        {
            var reader = checkScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var beforeCount = await reader.CountAsync(lotId, AppointmentTime, ct);
            Assert.Equal(1, beforeCount);
        }

        // Run worker
        await using (var workerScope = provider.CreateAsyncScope())
        {
            var worker = workerScope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            var processed = await worker.ProcessNoShowsAsync(ct);
            Assert.Equal(1, processed);
        }

        // After no-show: held quota drops by 1 (becomes 0)
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var reader = verifyScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var afterCount = await reader.CountAsync(lotId, AppointmentTime, ct);
            Assert.Equal(0, afterCount);
        }
    }

    // =========================================================================
    // N-16: Outbox atomicity: if the save fails, no outbox row exists
    // and the reservation is still Confirmed.
    // =========================================================================
    [Fact]
    public async Task N_16_Outbox_Atomicity_SaveFails_NoOutboxRow_And_Reservation_Still_Confirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(15));

        // Create provider with failing repository to simulate save failure
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IReservationPaymentClient>(new TrackingPaymentClient());
        services.AddScoped<CancelReservationCommandHandler>();
        services.AddScoped<CheckInReservationCommandHandler>();
        services.Configure<ReservationNoShowOptions>(opt =>
        {
            opt.BatchSize = 50;
            opt.IntervalSeconds = 60;
        });

        // Replace IReservationRepository with a failing save wrapper
        var descriptor = services.First(d => d.ServiceType == typeof(IReservationRepository));
        services.Remove(descriptor);
        services.AddScoped<IReservationRepository>(sp =>
        {
            var inner = (IReservationRepository)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
            return new FailingSaveReservationRepository(inner);
        });
        services.AddScoped<ReservationNoShowWorker>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51D" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed Confirmed reservation using a direct DbContext
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // Run worker pass: SaveChangesAsync fails with DbUpdateConcurrencyException.
        // Worker gracefully handles concurrency exception without throwing, skipping candidate.
        await using (var workerScope = provider.CreateAsyncScope())
        {
            var worker = workerScope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            var processed = await worker.ProcessNoShowsAsync(ct);
            Assert.Equal(0, processed);
        }

        // Verify database: no outbox row exists in PostgreSQL and reservation is still Confirmed
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reloaded = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Confirmed, reloaded.Status);
            Assert.False(reloaded.IsNoShow);

            var outboxCount = await db.OutboxMessages.AsNoTracking()
                .CountAsync(m => m.CorrelationId == reservationId, ct);
            Assert.Equal(0, outboxCount);
        }
    }

    private sealed class FailingSaveReservationRepository(IReservationRepository inner) : IReservationRepository
    {
        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            inner.FindByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken) =>
            inner.ListByUserIdAsync(userId, status, cancellationToken);

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken) =>
            inner.ListActiveByLotIdAsync(lotId, cancellationToken);

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken) =>
            inner.FindActiveConfirmedByLotAndPlateAsync(lotId, plateNumber, arrivalTime, cancellationToken);

        public void Add(Reservation reservation) => inner.Add(reservation);

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken) =>
            inner.ReloadAsync(reservation, cancellationToken);

        public void DiscardPendingChanges() => inner.DiscardPendingChanges();

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
            inner.ListPaymentReconciliationCandidatesAsync(now, batchSize, cancellationToken);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
            inner.ListNoShowCandidatesAsync(now, batchSize, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            throw new DbUpdateConcurrencyException("Simulated concurrency failure during save.");
        }
    }

    // =========================================================================
    // N-17: Worker run twice against the database -> 1 cancelled row, 1 outbox row.
    // =========================================================================
    [Fact]
    public async Task N_17_Worker_RunTwice_Produces_Exactly_One_Cancelled_Row_And_One_Outbox_Row()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(15));

        await using var provider = CreateProvider(clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51E" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed Confirmed reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // Pass 1
        await using (var scope1 = provider.CreateAsyncScope())
        {
            var worker = scope1.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            var count1 = await worker.ProcessNoShowsAsync(ct);
            Assert.Equal(1, count1);
        }

        // Pass 2
        await using (var scope2 = provider.CreateAsyncScope())
        {
            var worker = scope2.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            var count2 = await worker.ProcessNoShowsAsync(ct);
            Assert.Equal(0, count2);
        }

        // Verify final DB state
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Cancelled, res.Status);
            Assert.Equal(CancelReason.NoShow, res.CancelReason);
            Assert.True(res.IsNoShow);
            Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);

            var outboxMessages = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == reservationId && m.EventType == "booking.reservation-cancelled.v1")
                .ToListAsync(ct);

            Assert.Single(outboxMessages);
        }
    }
}
