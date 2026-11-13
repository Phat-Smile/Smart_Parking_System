extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Payments;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

using BookingReservationEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints;
using BookingCancelReservationApiRequest = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.CancelReservationApiRequest;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class HoldWhileDrivingCancellationIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class TrackingPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public Result<ReservationDebitState> RefundResultToReturn { get; set; } = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

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
            return Task.FromResult(RefundResultToReturn);
        }
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class
        {
            lock (Events)
            {
                Events.Add(integrationEvent);
            }
        }
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public string SubjectId => userId.ToString();
        public string? Email => "customer@test.com";
        public string? Role => "Customer";
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public static Guid CurrentUserId { get; set; } = Guid.NewGuid();

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, CurrentUserId.ToString()),
                new Claim("sub", CurrentUserId.ToString()),
                new Claim(ClaimTypes.Role, "Customer")
            ], "TestScheme");

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class CancelSender(CancelReservationCommandHandler handler) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CancelReservationCommand cmd)
            {
                return (Task<TResponse>)(object)handler.Handle(cmd, cancellationToken);
            }
            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
        services.AddSingleton<IOutboxWriter, RecordingOutbox>();
        services.AddScoped<CancelReservationCommandHandler>();
        services.AddScoped<CheckInReservationCommandHandler>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureMigratedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // =========================================================================
    // C06-14: Cancel and internal check-in race on the same Confirmed reservation
    // -> final state is exactly one of Used or Cancelled.
    // If Used: 0 refunds. If Cancelled within 5 minutes: exactly 1 refund.
    // =========================================================================
    [Fact]
    public async Task C06_14_Cancel_And_CheckIn_Race_Ends_In_Exactly_One_State_And_At_Most_One_Refund()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var appointmentTime = Now.AddMinutes(20);

        // Seed a Confirmed HoldWhileDriving reservation
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
                ReservationType.HoldWhileDriving,
                appointmentTime,
                50000m,
                Now.AddMinutes(-2),
                tempHoldExpiresAt: Now.AddMinutes(8));
            res.Confirm(Now.AddMinutes(-1));
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // Two concurrent racers: Cancel vs CheckIn
        var barrier = new Barrier(2);

        async Task RunCancelAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();
            barrier.SignalAndWait(ct);
            await handler.Handle(new CancelReservationCommand(reservationId, userId, "Cancel race"), ct);
        }

        async Task RunCheckInAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CheckInReservationCommandHandler>();
            barrier.SignalAndWait(ct);
            await handler.Handle(new CheckInReservationCommand(lotId, plateNumber, Now, Guid.NewGuid()), ct);
        }

        var cancelTask = Task.Run(RunCancelAsync, ct);
        var checkInTask = Task.Run(RunCheckInAsync, ct);

        await Task.WhenAll(cancelTask, checkInTask);

        // Verify final DB state
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var finalRes = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);

            // Exactly one of Used or Cancelled
            Assert.True(finalRes.Status == ReservationStatus.Used ^ finalRes.Status == ReservationStatus.Cancelled,
                $"Expected either Used or Cancelled, but was {finalRes.Status}");

            if (finalRes.Status == ReservationStatus.Used)
            {
                // If Used: 0 refunds
                Assert.Empty(paymentClient.RefundCalls);
            }
            else
            {
                // If Cancelled within 5 minutes: exactly 1 refund
                Assert.Single(paymentClient.RefundCalls);
                Assert.Equal(PrepaidOutcome.Refunded, finalRes.PrepaidOutcome);
            }
        }
    }

    // =========================================================================
    // C06-15: Two concurrent cancels -> one succeeds, at most one refund call
    // =========================================================================
    [Fact]
    public async Task C06_15_Two_Concurrent_Cancels_One_Succeeds_At_Most_One_Refund()
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
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                Guid.NewGuid(),
                "51B" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId,
                VehicleGroup.Car,
                ReservationType.HoldWhileDriving,
                Now.AddMinutes(20),
                50000m,
                Now.AddMinutes(-2),
                tempHoldExpiresAt: Now.AddMinutes(8));
            res.Confirm(Now.AddMinutes(-1));
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        var barrier = new Barrier(2);

        async Task<Result<CancelledReservationDto>> RunCancelAsync(string reason)
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();
            barrier.SignalAndWait(ct);
            return await handler.Handle(new CancelReservationCommand(reservationId, userId, reason), ct);
        }

        var task1 = Task.Run(() => RunCancelAsync("Cancel 1"), ct);
        var task2 = Task.Run(() => RunCancelAsync("Cancel 2"), ct);

        var results = await Task.WhenAll(task1, task2);

        // Exactly one succeeds
        Assert.Single(results.Where(r => r.IsSuccess));
        // Exactly one fails with CannotCancel
        Assert.Single(results.Where(r => r.IsFailure && r.Error == ReservationErrors.CannotCancel));

        // At most one refund call
        Assert.Single(paymentClient.RefundCalls);

        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var finalRes = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Cancelled, finalRes.Status);
            Assert.Equal(PrepaidOutcome.Refunded, finalRes.PrepaidOutcome);
        }
    }

    // =========================================================================
    // C06-16: Refund pending -> run worker -> exactly 1 refund, outcome Refunded;
    // second worker run -> 0 new refunds.
    // =========================================================================
    [Fact]
    public async Task C06_16_Refund_Pending_ReconciliationWorker_Runs_Once_Refunds_Second_Run_Zero_Refunds()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        // Seed a reservation that is Cancelled with refund pending (ConfirmedAt != null, PrepaidOutcome == null)
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                Guid.NewGuid(),
                "51C" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId,
                VehicleGroup.Car,
                ReservationType.HoldWhileDriving,
                Now.AddMinutes(20),
                50000m,
                Now.AddMinutes(-10),
                tempHoldExpiresAt: Now.AddMinutes(8));
            res.Confirm(Now.AddMinutes(-5));
            res.Cancel(Now.AddMinutes(-2), CancelReason.CustomerCancelled, null); // pending refund
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        var options = Options.Create(new ReservationPaymentReconciliationOptions { BatchSize = 50 });
        var worker = new ReservationPaymentReconciliationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<ReservationPaymentReconciliationWorker>.Instance);

        // Run worker pass 1
        var processed1 = await worker.ReconcileBatchAsync(ct);
        Assert.Equal(1, processed1);
        Assert.Single(paymentClient.RefundCalls);
        Assert.Equal(50000L, paymentClient.RefundCalls[0].AmountVnd);
        Assert.Equal(reservationId, paymentClient.RefundCalls[0].ReservationId);

        // Verify entity outcome updated in DB
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var updated = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, ct);
            Assert.Equal(ReservationStatus.Cancelled, updated.Status);
            Assert.Equal(PrepaidOutcome.Refunded, updated.PrepaidOutcome);
        }

        // Reset payment call tracker
        paymentClient.RefundCalls.Clear();

        // Run worker pass 2 -> 0 candidates, 0 refunds
        var processed2 = await worker.ReconcileBatchAsync(ct);
        Assert.Equal(0, processed2);
        Assert.Empty(paymentClient.RefundCalls);
    }

    // =========================================================================
    // C06-17: HTTP POST /api/booking/reservations/{id}/cancel
    // -> 200 refund, 200 forfeit, 409 Used, 403 other user, 404 unknown.
    // =========================================================================
    [Fact]
    public async Task C06_17_Http_Post_Cancel_Returns_Expected_Status_Codes()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        // Res 1: eligible for refund (< 5 min)
        var resRefundId = Guid.NewGuid();
        // Res 2: forfeit (> 5 min)
        var resForfeitId = Guid.NewGuid();
        // Res 3: already used
        var resUsedId = Guid.NewGuid();

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var r1 = Reservation.CreateTemporaryHold(
                resRefundId, userId, Guid.NewGuid(), "51D11111", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 50000m, Now.AddMinutes(-2));
            r1.Confirm(Now.AddMinutes(-2)); // confirmed 2 min ago -> refund

            var r2 = Reservation.CreateTemporaryHold(
                resForfeitId, userId, Guid.NewGuid(), "51D22222", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 50000m, Now.AddMinutes(-10));
            r2.Confirm(Now.AddMinutes(-10)); // confirmed 10 min ago -> forfeit

            var r3 = Reservation.CreateTemporaryHold(
                resUsedId, userId, Guid.NewGuid(), "51D33333", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 50000m, Now.AddMinutes(-10));
            r3.Confirm(Now.AddMinutes(-10));
            r3.Use(Now.AddMinutes(-1)); // Used

            db.Reservations.AddRange(r1, r2, r3);
            await db.SaveChangesAsync(ct);
        }

        // Build WebApplication with TestServer
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null);
        builder.Services.AddAuthorization();

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        builder.Services.AddPersistence(configuration);
        builder.Services.AddSingleton<IClock>(clock);
        builder.Services.AddSingleton<IReservationPaymentClient>(paymentClient);
        builder.Services.AddSingleton<IOutboxWriter, RecordingOutbox>();
        builder.Services.AddScoped<CancelReservationCommandHandler>();
        builder.Services.AddScoped<ISender, CancelSender>();
        builder.Services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(TestAuthHandler.CurrentUserId));

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        BookingReservationEndpoints.MapReservationEndpoints(app);
        await app.StartAsync(ct);

        var client = app.GetTestClient();

        // 1. 200 refund
        TestAuthHandler.CurrentUserId = userId;
        var respRefund = await client.PostAsJsonAsync($"/api/booking/reservations/{resRefundId}/cancel",
            new BookingCancelReservationApiRequest("Cancel early"), ct);
        Assert.Equal(HttpStatusCode.OK, respRefund.StatusCode);
        var bodyRefund = await respRefund.Content.ReadFromJsonAsync<CancelledReservationDto>(ct);
        Assert.NotNull(bodyRefund);
        Assert.Equal(50000m, bodyRefund.RefundAmount);
        Assert.Contains("hoàn về ví", bodyRefund.Message);

        // 2. 200 forfeit
        TestAuthHandler.CurrentUserId = userId;
        var respForfeit = await client.PostAsJsonAsync($"/api/booking/reservations/{resForfeitId}/cancel",
            new BookingCancelReservationApiRequest("Cancel late"), ct);
        Assert.Equal(HttpStatusCode.OK, respForfeit.StatusCode);
        var bodyForfeit = await respForfeit.Content.ReadFromJsonAsync<CancelledReservationDto>(ct);
        Assert.NotNull(bodyForfeit);
        Assert.Equal(0m, bodyForfeit.RefundAmount);
        Assert.Contains("không được hoàn cọc", bodyForfeit.Message);

        // 3. 409 Used
        TestAuthHandler.CurrentUserId = userId;
        var respUsed = await client.PostAsJsonAsync($"/api/booking/reservations/{resUsedId}/cancel",
            new BookingCancelReservationApiRequest("Cancel used"), ct);
        Assert.Equal(HttpStatusCode.Conflict, respUsed.StatusCode);

        // 4. 403 other user
        TestAuthHandler.CurrentUserId = otherUserId;
        var unknownResId = Guid.NewGuid();
        // Seed another reservation owned by userId to test 403
        var resOwnedByUserId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var r4 = Reservation.CreateTemporaryHold(
                resOwnedByUserId, userId, Guid.NewGuid(), "51D44444", lotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), 0m, Now.AddMinutes(-2));
            db.Reservations.Add(r4);
            await db.SaveChangesAsync(ct);
        }

        var resp403 = await client.PostAsJsonAsync($"/api/booking/reservations/{resOwnedByUserId}/cancel",
            new BookingCancelReservationApiRequest("Cancel someone else's"), ct);
        Assert.Equal(HttpStatusCode.Forbidden, resp403.StatusCode);

        // 5. 404 unknown
        TestAuthHandler.CurrentUserId = userId;
        var resp404 = await client.PostAsJsonAsync($"/api/booking/reservations/{unknownResId}/cancel",
            new BookingCancelReservationApiRequest("Cancel unknown"), ct);
        Assert.Equal(HttpStatusCode.NotFound, resp404.StatusCode);
    }

    // =========================================================================
    // C06-18: After cancel, held quota for the lot drops by 1 (slot returned)
    // =========================================================================
    [Fact]
    public async Task C06_18_After_Cancel_Held_Quota_For_Lot_Drops_By_One()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(Now);
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var arrivalAt = Now.AddMinutes(20);

        // Seed a Confirmed HoldWhileDriving reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                Guid.NewGuid(),
                "51E" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId,
                VehicleGroup.Car,
                ReservationType.HoldWhileDriving,
                arrivalAt,
                50000m,
                Now.AddMinutes(-2),
                tempHoldExpiresAt: Now.AddMinutes(8));
            res.Confirm(Now.AddMinutes(-1));
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // Before cancel: held quota reader sees 1 active reservation
        await using (var checkScope = provider.CreateAsyncScope())
        {
            var reader = checkScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var beforeCount = await reader.CountAsync(lotId, arrivalAt, ct);
            Assert.Equal(1, beforeCount);
        }

        // Perform cancellation
        await using (var cancelScope = provider.CreateAsyncScope())
        {
            var handler = cancelScope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();
            var result = await handler.Handle(new CancelReservationCommand(reservationId, userId, "Cancel to return slot"), ct);
            Assert.True(result.IsSuccess);
        }

        // After cancel: held quota drops by 1 (becomes 0)
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var reader = verifyScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var afterCount = await reader.CountAsync(lotId, arrivalAt, ct);
            Assert.Equal(0, afterCount);
        }
    }
}
