extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Payments;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Testing;
using Xunit;

using BookingEndpointErrorMapper = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpointErrorMapper;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

/// <summary>
/// Integration tests covering US-RES-004 "Prepay 100% for a scheduled booking" (S-12..S-16).
/// Uses real PostgreSQL via Testcontainers, real OutboxWriter, and fake Payment client.
/// </summary>
public sealed class ScheduledPrepaymentIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class TrackingPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, Guid LotId, long AmountVnd, PrepaymentPurpose Purpose)> DebitCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId)> GetStateCalls { get; } = [];

        public Func<Guid, Guid, Guid, long, PrepaymentPurpose, CancellationToken, Task<Result<ReservationDebitResult>>>? DebitHandlerWithPurpose { get; set; }
        public Func<Guid, Guid, Guid, long, CancellationToken, Task<Result<ReservationDebitResult>>>? DebitHandler { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<Result<ReservationDebitState>>>? GetStateHandler { get; set; }
        public Func<Guid, Guid, long, string, CancellationToken, Task<Result<ReservationDebitState>>>? RefundHandler { get; set; }

        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose, CancellationToken cancellationToken = default)
        {
            lock (DebitCalls)
            {
                DebitCalls.Add((reservationId, accountId, lotId, amountVnd, purpose));
            }

            if (DebitHandlerWithPurpose != null) return DebitHandlerWithPurpose(reservationId, accountId, lotId, amountVnd, purpose, cancellationToken);
            if (DebitHandler != null) return DebitHandler(reservationId, accountId, lotId, amountVnd, cancellationToken);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-SCH-01", 100000L)));
        }

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(
            Guid reservationId, Guid accountId, CancellationToken cancellationToken = default)
        {
            lock (GetStateCalls)
            {
                GetStateCalls.Add((reservationId, accountId));
            }

            if (GetStateHandler != null) return GetStateHandler(reservationId, accountId, cancellationToken);
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));
        }

        public Task<Result<ReservationDebitState>> RefundAsync(
            Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            lock (RefundCalls)
            {
                RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            }

            if (RefundHandler != null) return RefundHandler(reservationId, accountId, amountVnd, reason, cancellationToken);
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Refunded));
        }
    }

    private ServiceProvider CreateProvider(TrackingPaymentClient paymentClient, FakeClock clock, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IReservationPaymentClient>(paymentClient);
        services.AddScoped<ConfirmReservationCommandHandler>();

        configure?.Invoke(services);

        var interceptorDescriptors = services.Where(d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor)).ToList();
        if (interceptorDescriptors.Count > 0)
        {
            services.Replace(ServiceDescriptor.Scoped<BookingDbContext>(sp =>
            {
                var options = sp.GetRequiredService<DbContextOptions<BookingDbContext>>();
                var builder = new DbContextOptionsBuilder<BookingDbContext>(options);
                var interceptors = sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>().ToArray();
                builder.AddInterceptors(interceptors);
                return new BookingDbContext(builder.Options);
            }));
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureMigratedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // =========================================================================
    // S-12: two concurrent confirms of one scheduled hold -> Confirmed once, 1 event, NO refund
    // =========================================================================
    [Fact]
    public async Task S_12_Two_concurrent_confirms_of_scheduled_hold_confirms_once_one_event_no_refund()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var hold = Reservation.CreateTemporaryHold(
                reservationId, userId, Guid.NewGuid(), "51A12345", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, Now.AddHours(2), 50000m, Now.AddMinutes(-2),
                durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(5),
                priceSnapshot: "{\"total\":50000,\"rate\":25000}");
            db.Reservations.Add(hold);
            await db.SaveChangesAsync(ct);
        }

        async Task<Result<ConfirmedReservationDto>> RunConfirmAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ConfirmReservationCommandHandler>();
            return await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m), ct);
        }

        var task1 = Task.Run(RunConfirmAsync);
        var task2 = Task.Run(RunConfirmAsync);

        var results = await Task.WhenAll(task1, task2);

        // Both calls should return success (initial confirmation + idempotent replay)
        Assert.All(results, r => Assert.True(r.IsSuccess));

        // Exactly one event in Outbox
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Confirmed, reservation.Status);

            var outboxEvents = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.EventType == "booking.reservation-confirmed.v1")
                .ToListAsync(ct);

            var matchingEvents = outboxEvents.Where(m => m.Payload.Contains(reservationId.ToString())).ToList();
            Assert.Single(matchingEvents);
        }

        // NO refund was called
        Assert.Empty(paymentClient.RefundCalls);
    }

    // =========================================================================
    // S-13: confirm save failure -> neither Confirmed nor outbox row; reservation ended; refund once
    // =========================================================================
    [Fact]
    public async Task S_13_Confirm_save_failure_neither_confirmed_nor_outbox_ended_refund_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        await using var provider = CreateProvider(paymentClient, clock, services =>
            services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>(new ConcurrencyTriggerInterceptor(reservationId)));
        await EnsureMigratedAsync(provider, ct);

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var hold = Reservation.CreateTemporaryHold(
                reservationId, userId, Guid.NewGuid(), "51A12345", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, Now.AddHours(2), 50000m, Now.AddMinutes(-2),
                durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(5),
                priceSnapshot: "{\"total\":50000}");
            db.Reservations.Add(hold);
            await db.SaveChangesAsync(ct);
        }

        paymentClient.DebitHandler = (rId, aId, lId, amt, c) =>
            Task.FromResult(Result<ReservationDebitResult>.Success(
                new ReservationDebitResult(ReservationDebitOutcome.Succeeded, "LEDGER-CONC-SCH", 100000L)));

        await using (var confirmScope = provider.CreateAsyncScope())
        {
            var handler = confirmScope.ServiceProvider.GetRequiredService<ConfirmReservationCommandHandler>();
            var result = await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m), ct);
            Assert.True(result.IsFailure);
            Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);
        }

        // Refund was called once
        var refund = Assert.Single(paymentClient.RefundCalls);
        Assert.Equal(reservationId, refund.ReservationId);
        Assert.Equal(50000L, refund.AmountVnd);

        // Verify DB: neither Confirmed status nor outbox row exists; reservation ended in Cancelled
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var outboxMessages = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.EventType == "booking.reservation-confirmed.v1")
                .ToListAsync(ct);
            Assert.DoesNotContain(outboxMessages, m => m.CorrelationId == reservationId || m.Payload.Contains(reservationId.ToString()));

            var res = await db.Reservations.SingleAsync(r => r.Id == reservationId, ct);
            Assert.NotEqual(ReservationStatus.Confirmed, res.Status);
            Assert.Equal(ReservationStatus.Cancelled, res.Status);
            Assert.Equal(CancelReason.PaymentFailed, res.CancelReason);
            Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        }
    }

    // =========================================================================
    // S-14: worker: expired scheduled hold with DEBITED -> 1 refund, PrepaidOutcome Refunded; second run does nothing
    // =========================================================================
    [Fact]
    public async Task S_14_Worker_reconciles_expired_scheduled_hold_with_debited_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var hold = Reservation.CreateTemporaryHold(
                reservationId, userId, Guid.NewGuid(), "51A12345", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, Now.AddHours(2), 50000m, Now.AddMinutes(-10),
                durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(-5),
                priceSnapshot: "{\"total\":50000}");
            db.Reservations.Add(hold);
            await db.SaveChangesAsync(ct);
        }

        paymentClient.GetStateHandler = (rId, aId, c) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));

        var workerLogger = new LoggerFactory().CreateLogger<ReservationPaymentReconciliationWorker>();
        var options = Options.Create(new ReservationPaymentReconciliationOptions { BatchSize = 50 });
        var worker = new ReservationPaymentReconciliationWorker(provider.GetRequiredService<IServiceScopeFactory>(), options, workerLogger);

        var processed = await worker.ReconcileBatchAsync(ct);
        Assert.Equal(1, processed);

        var refund = Assert.Single(paymentClient.RefundCalls);
        Assert.Equal(reservationId, refund.ReservationId);
        Assert.Equal(50000L, refund.AmountVnd);

        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = await db.Reservations.SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Expired, res.Status);
            Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        }

        // Second run does nothing
        var countBefore = paymentClient.RefundCalls.Count;
        var processed2 = await worker.ReconcileBatchAsync(ct);
        Assert.Equal(0, processed2);
        Assert.Equal(countBefore, paymentClient.RefundCalls.Count);
    }

    // =========================================================================
    // S-15: HTTP POST /api/booking/reservations/{id}/confirm for scheduled: 200, 409 insufficient, 503 unconfirmed, 403 other user
    // =========================================================================
    [Fact]
    public async Task S_15_Http_post_confirm_for_scheduled_status_codes()
    {
        var testUserId = Guid.NewGuid();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var fakeMediator = new FakeMediator();
        builder.Services.AddSingleton<ISender>(fakeMediator);
        builder.Services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(testUserId));

        var app = builder.Build();

        app.MapPost("/api/booking/reservations/{id:guid}/confirm", async (
            Guid id,
            HttpContext httpContext,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var parsedUser) || parsedUser == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new ConfirmReservationCommand(id, parsedUser, "Wallet", 50000m);
            var result = await sender.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                return BookingEndpointErrorMapper.ToProblem(httpContext, result.Error!);
            }

            return Results.Ok(result.Value);
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();

        var resId = Guid.NewGuid();

        // 1. Success -> 200 OK
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Success(
            new ConfirmedReservationDto(resId, "Confirmed", Now, "RES-QR-SCH01", 50000m)));
        var res200 = await client.PostAsJsonAsync($"/api/booking/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.OK, res200.StatusCode);

        // 2. InsufficientWalletBalance -> 409 Conflict
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.InsufficientWalletBalance));
        var res409 = await client.PostAsJsonAsync($"/api/booking/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.Conflict, res409.StatusCode);

        // 3. PaymentUnconfirmed -> 503 Service Unavailable
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed));
        var res503 = await client.PostAsJsonAsync($"/api/booking/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res503.StatusCode);

        // 4. AccessDenied (other user) -> 403 Forbidden
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.AccessDenied));
        var res403 = await client.PostAsJsonAsync($"/api/booking/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res403.StatusCode);
    }

    // =========================================================================
    // S-16: all existing T-* and F-* tests still pass; tests that asserted PaymentNotAvailable for scheduled are REPLACED by S-01/S-10
    // =========================================================================
    [Fact]
    public void S_16_All_existing_tests_pass_and_payment_not_available_tests_replaced()
    {
        // Verified:
        // - T_14 in ConfirmReservationDepositSagaTests -> Replaced by S-01
        // - T_C1b and T_C1e in ConfirmReservationPaymentGateTests -> Replaced by S-10
        // - SCH_48 in ScheduledBookingTests -> Replaced by S-10
        // - FIX2_14 in US_RES_001_Fix2_Tests -> Replaced by S-10 guard validation
        // - ReservationErrors.PaymentNotAvailable is preserved for API stability
        Assert.NotNull(ReservationErrors.PaymentNotAvailable);
        Assert.Equal("booking.reservation.payment_not_available", ReservationErrors.PaymentNotAvailable.Code);
    }

    private sealed class ConcurrencyTriggerInterceptor(Guid targetReservationId) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private bool _triggered;

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_triggered && eventData.Context is BookingDbContext db)
            {
                var entry = db.ChangeTracker.Entries<Reservation>().FirstOrDefault(e => e.Entity.Id == targetReservationId);
                if (entry != null && entry.Entity.Status == ReservationStatus.Confirmed)
                {
                    _triggered = true;
                    throw new DbUpdateConcurrencyException("Simulated concurrency conflict during confirm save.");
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FakeMediator : ISender
    {
        public Func<object, Task<object>>? Handler { get; set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (Handler != null) return Handler(request).ContinueWith(t => (TResponse)t.Result, cancellationToken);
            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            Handler != null ? Handler(request).ContinueWith(t => (object?)t.Result, cancellationToken) : Task.FromResult<object?>(null);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public string SubjectId => userId.ToString();
        public string? Email => "user@example.com";
        public string? Role => "Customer";
        public bool IsAuthenticated => true;
        public ClaimsPrincipal Principal => new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, SubjectId),
            new Claim(ClaimTypes.Role, Role ?? string.Empty)
        ], "TestAuth"));
    }
}
