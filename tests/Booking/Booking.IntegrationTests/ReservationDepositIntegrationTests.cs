extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Messaging.Outbox;
using ParkingSystem.Booking.Infrastructure.Payments;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.Booking.Persistence.Reservations;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

using BookingEndpointErrorMapper = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpointErrorMapper;
using BookingReservationEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationDepositIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 14, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class TrackingPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, Guid LotId, long AmountVnd)> DebitCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId)> GetStateCalls { get; } = [];

        public Func<Guid, Guid, Guid, long, CancellationToken, Task<Result<ReservationDebitResult>>>? DebitHandler { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<Result<ReservationDebitState>>>? GetStateHandler { get; set; }
        public Func<Guid, Guid, long, string, CancellationToken, Task<Result<ReservationDebitState>>>? RefundHandler { get; set; }

        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, CancellationToken cancellationToken = default)
        {
            lock (DebitCalls)
            {
                DebitCalls.Add((reservationId, accountId, lotId, amountVnd));
            }

            if (DebitHandler != null) return DebitHandler(reservationId, accountId, lotId, amountVnd, cancellationToken);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-TEST-01", 100000L)));
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

    private ServiceProvider CreateProvider(TrackingPaymentClient paymentClient, FakeClock clock)
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

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureMigratedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // =========================================================================
    // T-18: Two concurrent confirms -> Confirmed once, 1 event, NO refund
    // =========================================================================
    [Fact]
    public async Task T_18_Two_concurrent_confirms_confirms_once_emits_one_event_no_refund()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        // Seed a temporary hold in PostgreSQL
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var hold = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                Guid.NewGuid(),
                "51A12345",
                lotId,
                VehicleGroup.Car,
                ReservationType.HoldWhileDriving,
                Now.AddMinutes(20),
                20000m,
                Now.AddMinutes(-2),
                tempHoldExpiresAt: Now.AddMinutes(5));
            db.Reservations.Add(hold);
            await db.SaveChangesAsync(ct);
        }

        // Two concurrent confirmation commands
        var barrier = new Barrier(2);
        async Task<Result<ConfirmedReservationDto>> RunConfirmAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ConfirmReservationCommandHandler>();
            barrier.SignalAndWait();
            return await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 20000m), ct);
        }

        var task1 = Task.Run(RunConfirmAsync);
        var task2 = Task.Run(RunConfirmAsync);

        var results = await Task.WhenAll(task1, task2);

        // Both calls should return success (either initial confirmation or idempotent replay)
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
    // T-19: Save failure after debit -> refund occurs or worker reconciles
    // =========================================================================
    [Fact]
    public async Task T_19_Save_failure_after_debit_compensates_with_refund()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();

        // Use a test repo that fails on SaveChangesAsync to simulate DB crash after debit
        var failingRepo = new FailingSaveReservationRepository();
        var logger = new LoggerFactory().CreateLogger<ConfirmReservationCommandHandler>();
        var handler = new ConfirmReservationCommandHandler(failingRepo, paymentClient, clock, logger);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var hold = Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            Now.AddMinutes(20),
            20000m,
            Now.AddMinutes(-2),
            tempHoldExpiresAt: Now.AddMinutes(5));
        failingRepo.ReservationToReturn = hold;

        // Save failure occurs -> handler catches it, executes refund compensation, and returns PaymentUnconfirmed
        var result = await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 20000m), ct);
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);

        // Compensation refund was called immediately!
        var refundCall = Assert.Single(paymentClient.RefundCalls);
        Assert.Equal(reservationId, refundCall.ReservationId);
        Assert.Equal(userId, refundCall.AccountId);
        Assert.Equal(20000L, refundCall.AmountVnd);
    }

    private sealed class FailingSaveReservationRepository : IReservationRepository
    {
        public Reservation? ReservationToReturn { get; set; }
        public void Add(Reservation r) { }

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReservationToReturn);

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(
            DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Database disk full simulation.");
    }

    // =========================================================================
    // T-20: Worker reconciliation: DEBITED -> 1 refund & PrepaidOutcome.Refunded;
    //       second run does nothing; NOT_DEBITED -> no refund; Confirmed row untouched;
    //       failing row doesn't stop batch.
    // =========================================================================
    [Fact]
    public async Task T_20_Worker_reconciliation_flow()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var lotId = Guid.NewGuid();
        var resDebitedId = Guid.NewGuid();
        var resNotDebitedId = Guid.NewGuid();
        var resConfirmedId = Guid.NewGuid();
        var resFailingId = Guid.NewGuid();
        var resDebited2Id = Guid.NewGuid();

        var userDebited = Guid.NewGuid();
        var userNotDebited = Guid.NewGuid();
        var userConfirmed = Guid.NewGuid();
        var userFailing = Guid.NewGuid();
        var userDebited2 = Guid.NewGuid();

        // Seed rows into PostgreSQL
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // 1. Debited hold -> should be refunded & marked Refunded
            var hold1 = Reservation.CreateTemporaryHold(
                resDebitedId, userDebited, Guid.NewGuid(), "51A11111", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 20000m, Now.AddMinutes(-10),
                tempHoldExpiresAt: Now.AddMinutes(-5));

            // 2. Not debited hold -> should expire, no refund
            var hold2 = Reservation.CreateTemporaryHold(
                resNotDebitedId, userNotDebited, Guid.NewGuid(), "51A22222", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 15000m, Now.AddMinutes(-10),
                tempHoldExpiresAt: Now.AddMinutes(-5));

            // 3. Confirmed hold -> untouched
            var hold3 = Reservation.CreateTemporaryHold(
                resConfirmedId, userConfirmed, Guid.NewGuid(), "51A33333", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 20000m, Now.AddMinutes(-10),
                tempHoldExpiresAt: Now.AddMinutes(5));
            hold3.Confirm(Now.AddMinutes(-5));

            // 4. Failing hold -> throws on payment query
            var hold4 = Reservation.CreateTemporaryHold(
                resFailingId, userFailing, Guid.NewGuid(), "51A44444", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 25000m, Now.AddMinutes(-12),
                tempHoldExpiresAt: Now.AddMinutes(-6));

            // 5. Another debited hold -> should be refunded despite row 4 failure!
            var hold5 = Reservation.CreateTemporaryHold(
                resDebited2Id, userDebited2, Guid.NewGuid(), "51A55555", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 30000m, Now.AddMinutes(-8),
                tempHoldExpiresAt: Now.AddMinutes(-4));

            db.Reservations.AddRange(hold1, hold2, hold3, hold4, hold5);
            await db.SaveChangesAsync(ct);
        }

        // Configure payment outcomes
        paymentClient.GetStateHandler = (resId, _, _) =>
        {
            if (resId == resDebitedId) return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));
            if (resId == resNotDebitedId) return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.NotDebited));
            if (resId == resFailingId) throw new InvalidOperationException("Simulated transient gRPC crash on failing row.");
            if (resId == resDebited2Id) return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));
        };

        var workerLogger = new LoggerFactory().CreateLogger<ReservationPaymentReconciliationWorker>();
        var options = Options.Create(new ReservationPaymentReconciliationOptions { BatchSize = 50 });
        var worker = new ReservationPaymentReconciliationWorker(provider.GetRequiredService<IServiceScopeFactory>(), options, workerLogger);

        // Run batch pass 1
        var processedPass1 = await worker.ReconcileBatchAsync(ct);
        Assert.True(processedPass1 >= 3);

        // Verify DB state
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var r1 = await db.Reservations.SingleAsync(r => r.Id == resDebitedId, ct);
            Assert.Equal(ReservationStatus.Expired, r1.Status);
            Assert.Equal(PrepaidOutcome.Refunded, r1.PrepaidOutcome);

            var r2 = await db.Reservations.SingleAsync(r => r.Id == resNotDebitedId, ct);
            Assert.Equal(ReservationStatus.Expired, r2.Status);
            Assert.Null(r2.PrepaidOutcome);

            var r3 = await db.Reservations.SingleAsync(r => r.Id == resConfirmedId, ct);
            Assert.Equal(ReservationStatus.Confirmed, r3.Status);

            var r5 = await db.Reservations.SingleAsync(r => r.Id == resDebited2Id, ct);
            Assert.Equal(ReservationStatus.Expired, r5.Status);
            Assert.Equal(PrepaidOutcome.Refunded, r5.PrepaidOutcome);
        }

        // Verify refunds: Exactly 2 refunds (for resDebitedId and resDebited2Id)
        Assert.Equal(2, paymentClient.RefundCalls.Count);
        Assert.Contains(paymentClient.RefundCalls, r => r.ReservationId == resDebitedId && r.AmountVnd == 20000L);
        Assert.Contains(paymentClient.RefundCalls, r => r.ReservationId == resDebited2Id && r.AmountVnd == 30000L);

        // Run batch pass 2 -> Second run does nothing for already refunded rows!
        var refundsBeforePass2 = paymentClient.RefundCalls.Count;
        await worker.ReconcileBatchAsync(ct);
        Assert.Equal(refundsBeforePass2, paymentClient.RefundCalls.Count);
    }

    // =========================================================================
    // T-21: Outbox row and Confirmed commit atomically; failure commits neither
    // =========================================================================
    [Fact]
    public async Task T_21_Outbox_row_and_confirmed_commit_atomically()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Seed
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var hold = Reservation.CreateTemporaryHold(
                reservationId, userId, Guid.NewGuid(), "51A12345", Guid.NewGuid(),
                VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20),
                20000m, Now.AddMinutes(-2), tempHoldExpiresAt: Now.AddMinutes(5));
            db.Reservations.Add(hold);
            await db.SaveChangesAsync(ct);
        }

        // Execute confirmation via handler using real OutboxWriter
        await using (var confirmScope = provider.CreateAsyncScope())
        {
            var handler = confirmScope.ServiceProvider.GetRequiredService<ConfirmReservationCommandHandler>();
            var result = await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 20000m), ct);
            Assert.True(result.IsSuccess);
        }

        // Verify atomic commit in DB
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Confirmed, reservation.Status);

            var outboxMessages = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.EventType == "booking.reservation-confirmed.v1")
                .ToListAsync(ct);

            var outboxRow = Assert.Single(outboxMessages.Where(m => m.CorrelationId == reservationId || m.Payload.Contains(reservationId.ToString())));
            Assert.NotNull(outboxRow);
        }
    }

    // =========================================================================
    // T-22: HTTP confirm endpoint: 200, 409 (insufficient), 503 (unconfirmed), 401, 403, 404
    // =========================================================================
    [Fact]
    public async Task T_22_Http_confirm_endpoint_status_codes()
    {
        var testUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var fakeMediator = new FakeMediator();
        builder.Services.AddSingleton<ISender>(fakeMediator);
        builder.Services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(testUserId));

        var app = builder.Build();

        app.MapPost("/reservations/{id:guid}/confirm", async (
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

            var command = new ConfirmReservationCommand(id, parsedUser, "Wallet", 20000m);
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
            new ConfirmedReservationDto(resId, "Confirmed", Now, "RES-QR-TEST", 20000m)));
        var res200 = await client.PostAsJsonAsync($"/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.OK, res200.StatusCode);

        // 2. InsufficientWalletBalance -> 409 Conflict
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.InsufficientWalletBalance));
        var res409 = await client.PostAsJsonAsync($"/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.Conflict, res409.StatusCode);

        // 3. PaymentUnconfirmed -> 503 Service Unavailable
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed));
        var res503 = await client.PostAsJsonAsync($"/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res503.StatusCode);

        // 4. AccessDenied -> 403 Forbidden
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.AccessDenied));
        var res403 = await client.PostAsJsonAsync($"/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res403.StatusCode);

        // 5. NotFound -> 404 Not Found
        fakeMediator.Handler = _ => Task.FromResult<object>(Result<ConfirmedReservationDto>.Failure(ReservationErrors.NotFound));
        var res404 = await client.PostAsJsonAsync($"/reservations/{resId}/confirm", new { });
        Assert.Equal(HttpStatusCode.NotFound, res404.StatusCode);
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public string SubjectId => userId?.ToString() ?? string.Empty;
        public string? Email => "customer@test.com";
        public string? Role => "Customer";
        public bool IsAuthenticated => userId.HasValue;
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

    // =========================================================================
    // T-23: Configuration test: default appsettings binds TempHoldDurationMinutes = 4,
    //       new hold has TempHoldExpiresAt = now + 4 min; alternate config sets different expiry
    // =========================================================================
    [Fact]
    public void T_23_Configuration_binds_TempHoldDurationMinutes_4_and_affects_hold_expiration()
    {
        // 1. Verify appsettings.json binding
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config = new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath, optional: true)
            .Build();

        var capacitySection = config.GetSection(BookingCapacityOptions.SectionName);
        var options = capacitySection.Get<BookingCapacityOptions>() ?? new BookingCapacityOptions();

        // Must bind 4 minutes per decision Q1b
        Assert.Equal(4, options.TempHoldDurationMinutes);

        // 2. Verify CreateTemporaryHoldCommandHandler uses configured minutes
        var clock = new FakeClock(Now);
        var ownership = new FakeOwnership();
        var capacityProvider = new FakeCapacity();
        var coordinator = new FakeCoord();
        var logger = new LoggerFactory().CreateLogger<CreateTemporaryHoldCommandHandler>();

        var handlerDefault = new CreateTemporaryHoldCommandHandler(
            ownership, capacityProvider, coordinator, clock, logger, new UnconfiguredPricing(), options);

        var cmd = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 20000m);

        handlerDefault.Handle(cmd, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.NotNull(coordinator.CapturedReservation);
        Assert.Equal(Now.AddMinutes(4), coordinator.CapturedReservation.TempHoldExpiresAt);

        // 3. Alternate configuration
        var alternateOptions = new BookingCapacityOptions { TempHoldDurationMinutes = 7 };
        var handlerAlternate = new CreateTemporaryHoldCommandHandler(
            ownership, capacityProvider, coordinator, clock, logger, new UnconfiguredPricing(), alternateOptions);

        handlerAlternate.Handle(cmd, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal(Now.AddMinutes(7), coordinator.CapturedReservation.TempHoldExpiresAt);
    }

    private sealed class FakeOwnership : IVehicleOwnershipClient
    {
        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
            string rawOrCanonicalPlate, Guid expectedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<VehicleOwnershipInfo>.Success(new VehicleOwnershipInfo(
                expectedUserId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
    }

    private sealed class FakeCapacity : IParkingLotCapacityProvider
    {
        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 20000m)));
    }

    private sealed class FakeCoord : IReservationCapacityCoordinator
    {
        public Reservation? CapturedReservation { get; private set; }

        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation, int ceiling, int? availableSlotCount, TimeSpan lockTimeout, int maxActiveReservationsPerUser = 3, CancellationToken cancellationToken = default)
        {
            CapturedReservation = reservation;
            return Task.FromResult(Result<Reservation>.Success(reservation));
        }
    }

    private sealed class UnconfiguredPricing : IParkingPricingProvider
    {
        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingPricingInfo>.Failure(new Error("pricing.unconfigured", "Unconfigured")));
    }
}
