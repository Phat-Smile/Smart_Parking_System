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
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Testing;
using Xunit;

using BookingReservationEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints;
using BookingCancelReservationApiRequest = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.CancelReservationApiRequest;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ScheduledReservationCancellationIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset AppointmentTime = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ConfirmedAtTime = AppointmentTime.AddHours(-3);

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
    // C07-10: HTTP cancel of a Scheduled reservation
    // -> 200 refund at A-31, 200 forfeit at A-10, 409 when Used.
    // =========================================================================
    [Fact]
    public async Task C07_10_Http_Cancel_Scheduled_Reservation_Returns_200Refund_200Forfeit_409Used()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(-31));
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var resRefundId = Guid.NewGuid();
        var resForfeitId = Guid.NewGuid();
        var resUsedId = Guid.NewGuid();

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // Res 1: Cancel at A - 31m (>= 30m before entry) -> 100% refund
            var r1 = Reservation.CreateTemporaryHold(
                resRefundId, userId, Guid.NewGuid(), "51S11111", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, AppointmentTime, 60000m, ConfirmedAtTime, durationMinutes: 120);
            r1.Confirm(ConfirmedAtTime);

            // Res 2: Cancel at A - 10m (< 30m before entry) -> forfeit
            var r2 = Reservation.CreateTemporaryHold(
                resForfeitId, userId, Guid.NewGuid(), "51S22222", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, AppointmentTime, 60000m, ConfirmedAtTime, durationMinutes: 120);
            r2.Confirm(ConfirmedAtTime);

            // Res 3: Already Used -> 409 CannotCancel
            var r3 = Reservation.CreateTemporaryHold(
                resUsedId, userId, Guid.NewGuid(), "51S33333", lotId, VehicleGroup.Car,
                ReservationType.Scheduled, AppointmentTime, 60000m, ConfirmedAtTime, durationMinutes: 120);
            r3.Confirm(ConfirmedAtTime);
            r3.Use(AppointmentTime.AddMinutes(-10));

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
        TestAuthHandler.CurrentUserId = userId;

        // 1. 200 Refund at A - 31m
        clock.UtcNow = AppointmentTime.AddMinutes(-31);
        var respRefund = await client.PostAsJsonAsync($"/api/booking/reservations/{resRefundId}/cancel",
            new BookingCancelReservationApiRequest("Cancel 31m before"), ct);
        Assert.Equal(HttpStatusCode.OK, respRefund.StatusCode);
        var bodyRefund = await respRefund.Content.ReadFromJsonAsync<CancelledReservationDto>(ct);
        Assert.NotNull(bodyRefund);
        Assert.Equal(60000m, bodyRefund.RefundAmount);
        Assert.Contains("Tiền đã trả trước đã được hoàn về ví của bạn", bodyRefund.Message);

        // 2. 200 Forfeit at A - 10m
        clock.UtcNow = AppointmentTime.AddMinutes(-10);
        var respForfeit = await client.PostAsJsonAsync($"/api/booking/reservations/{resForfeitId}/cancel",
            new BookingCancelReservationApiRequest("Cancel 10m before"), ct);
        Assert.Equal(HttpStatusCode.OK, respForfeit.StatusCode);
        var bodyForfeit = await respForfeit.Content.ReadFromJsonAsync<CancelledReservationDto>(ct);
        Assert.NotNull(bodyForfeit);
        Assert.Equal(0m, bodyForfeit.RefundAmount);
        Assert.Contains("không được hoàn tiền đã trả trước", bodyForfeit.Message);

        // 3. 409 Used
        var respUsed = await client.PostAsJsonAsync($"/api/booking/reservations/{resUsedId}/cancel",
            new BookingCancelReservationApiRequest("Cancel used"), ct);
        Assert.Equal(HttpStatusCode.Conflict, respUsed.StatusCode);
    }

    // =========================================================================
    // C07-11: Cancel vs check-in race on a Scheduled reservation
    // -> exactly one of Used or Cancelled; 0 refunds if Used.
    // =========================================================================
    [Fact]
    public async Task C07_11_Cancel_And_CheckIn_Race_On_Scheduled_Ends_In_One_State()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(-40));
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var plateNumber = "51S" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Seed a Confirmed Scheduled reservation
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
                60000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: AppointmentTime.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

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
            await handler.Handle(new CheckInReservationCommand(lotId, plateNumber, AppointmentTime.AddMinutes(-10), Guid.NewGuid()), ct);
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
                // If Cancelled at A - 40m: exactly 1 refund
                Assert.Single(paymentClient.RefundCalls);
                Assert.Equal(PrepaidOutcome.Refunded, finalRes.PrepaidOutcome);
            }
        }
    }

    // =========================================================================
    // C07-12: After cancel, held quota for the lot drops by 1
    // =========================================================================
    [Fact]
    public async Task C07_12_After_Cancel_Scheduled_Held_Quota_For_Lot_Drops_By_One()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(-35));
        var paymentClient = new TrackingPaymentClient();
        await using var provider = CreateProvider(paymentClient, clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var arrivalAt = AppointmentTime;

        // Seed a Confirmed Scheduled reservation
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                Guid.NewGuid(),
                "51S" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                arrivalAt,
                60000m,
                ConfirmedAtTime,
                durationMinutes: 120,
                tempHoldExpiresAt: arrivalAt.AddHours(-1));
            res.Confirm(ConfirmedAtTime);
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
            var result = await handler.Handle(new CancelReservationCommand(reservationId, userId, "Cancel scheduled to return slot"), ct);
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
