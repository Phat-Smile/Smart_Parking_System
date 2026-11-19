extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Workers;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

using BookingReservationEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints;
using BookingCancelForNoSuitableSlotApiRequest = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.CancelForNoSuitableSlotApiRequest;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class NoSuitableSlotIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string ExpectedArrivalKey = "test_arrival_secret_key_no_suitable_slot";
    private static readonly Guid UserId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly Guid VehicleId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string PlateNumber = "51A-123.45";

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string SubjectId => UserId.ToString();
        public string? Email => "user@smartparking.vn";
        public string? Role => "Customer";
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim("sub", UserId.ToString()),
                new Claim(ClaimTypes.Role, "Customer")
            ], "TestScheme");

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
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
        services.AddScoped<CancelForNoSuitableSlotCommandHandler>();
        services.AddScoped<CancelReservationCommandHandler>();
        services.AddScoped<HandleSlotStatusChangedCommandHandler>();
        services.Configure<ReservationNoShowOptions>(opt =>
        {
            opt.BatchSize = 50;
            opt.IntervalSeconds = 60;
        });
        services.AddScoped<ReservationNoShowWorker>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static Reservation CreateConfirmedReservation(
        Guid reservationId,
        Guid lotId,
        DateTimeOffset apptTime,
        decimal prepaidAmount = 50000m,
        string? plate = null,
        DateTimeOffset? confirmedAt = null)
    {
        var canonicalPlate = LicensePlateNormalizer.Normalize(plate ?? PlateNumber);
        var res = Reservation.CreateTemporaryHold(
            reservationId,
            UserId,
            VehicleId,
            canonicalPlate,
            lotId,
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            apptTime,
            prepaidAmount,
            apptTime.AddMinutes(-10));

        res.Confirm(confirmedAt ?? apptTime.AddMinutes(-2));
        return res;
    }

    // -------------------------------------------------------------------------
    // S-10: HTTP Security & Payload Validation (TestServer)
    // -------------------------------------------------------------------------
    private sealed class NoSuitableSlotSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CancelForNoSuitableSlotCommand)
            {
                var result = Result<CancelForNoSuitableSlotResult>.Success(new CancelForNoSuitableSlotResult(true, Guid.NewGuid()));
                return Task.FromResult((TResponse)(object)result);
            }

            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static async Task<WebApplication> CreateHttpAppAsync(string? arrivalKeyConfig)
    {
        var inMemoryConfig = new Dictionary<string, string?>();
        if (arrivalKeyConfig is not null)
        {
            inMemoryConfig["ArrivalIntegration:Key"] = arrivalKeyConfig;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(inMemoryConfig);
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null);
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ICurrentUser, FakeCurrentUser>();
        builder.Services.AddSingleton<ISender>(new NoSuitableSlotSender());

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        BookingReservationEndpoints.MapReservationEndpoints(app);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task S10_HttpSecurityAndValidation_EndpointsBehaveCorrectly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();

        // 1. 503 when ArrivalIntegration:Key is not configured
        {
            await using var unconfiguredApp = await CreateHttpAppAsync(null);
            var client = unconfiguredApp.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(lotId, PlateNumber, DateTimeOffset.UtcNow);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/no-suitable-slot")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

            var response = await client.SendAsync(req, cancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        // 2. 403 when X-Arrival-Service-Key is missing
        {
            await using var app = await CreateHttpAppAsync(ExpectedArrivalKey);
            var client = app.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(lotId, PlateNumber, DateTimeOffset.UtcNow);

            var response = await client.PostAsJsonAsync("/internal/booking/reservations/no-suitable-slot", payload, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // 3. 403 when X-Arrival-Service-Key has wrong key
        {
            await using var app = await CreateHttpAppAsync(ExpectedArrivalKey);
            var client = app.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(lotId, PlateNumber, DateTimeOffset.UtcNow);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/no-suitable-slot")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Arrival-Service-Key", "wrong_key_xyz");

            var response = await client.SendAsync(req, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // 4. 400 when body has empty LotId
        {
            await using var app = await CreateHttpAppAsync(ExpectedArrivalKey);
            var client = app.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(Guid.Empty, PlateNumber, DateTimeOffset.UtcNow);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/no-suitable-slot")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

            var response = await client.SendAsync(req, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // 5. 400 when body has empty PlateNumber
        {
            await using var app = await CreateHttpAppAsync(ExpectedArrivalKey);
            var client = app.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(lotId, "   ", DateTimeOffset.UtcNow);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/no-suitable-slot")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

            var response = await client.SendAsync(req, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // 6. 200 when valid key and payload
        {
            await using var app = await CreateHttpAppAsync(ExpectedArrivalKey);
            var client = app.GetTestClient();
            var payload = new BookingCancelForNoSuitableSlotApiRequest(lotId, PlateNumber, DateTimeOffset.UtcNow);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/no-suitable-slot")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

            var response = await client.SendAsync(req, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.True(json.GetProperty("cancelled").GetBoolean());
            Assert.True(json.TryGetProperty("reservationId", out var idProp));
            Assert.NotEqual(Guid.Empty, idProp.GetGuid());
        }
    }

    // -------------------------------------------------------------------------
    // S-11: Worker race at cutoff against real PostgreSQL
    // -------------------------------------------------------------------------
    [Fact]
    public async Task S11_CancelForNoSuitableSlot_Vs_NoShowWorker_RaceAtCutoff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        // Cutoff is A + 16:00. At 14:16, worker considers it eligible for NoShow.
        var clock = new FakeClock(apptTime.AddMinutes(16));
        var paymentClient = new TrackingPaymentClient();

        await using var provider = CreateProvider(clock, paymentClient);
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var resId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateConfirmedReservation(resId, lotId, apptTime, 50000m);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        var barrier = new Barrier(2);

        async Task RunApiCancelAsync()
        {
            try
            {
                await using var scope = provider.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<CancelForNoSuitableSlotCommandHandler>();
                barrier.SignalAndWait(cancellationToken);
                // Car arrived just before cutoff (at 14:15, inside arrival window)
                await handler.Handle(new CancelForNoSuitableSlotCommand(lotId, PlateNumber, apptTime.AddMinutes(15)), cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.InnerException?.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Expected concurrency loss when worker wins
            }
        }

        async Task RunWorkerAsync()
        {
            try
            {
                await using var scope = provider.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
                barrier.SignalAndWait(cancellationToken);
                await worker.ProcessNoShowsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.InnerException?.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Expected concurrency loss when API cancel wins
            }
        }

        var apiTask = Task.Run(RunApiCancelAsync, cancellationToken);
        var workerTask = Task.Run(RunWorkerAsync, cancellationToken);

        await Task.WhenAll(apiTask, workerTask);

        // Verify database state: exactly one winner
        await using (var assertScope = provider.CreateAsyncScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resId, cancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(ReservationStatus.Cancelled, loaded.Status);

            // Exactly one winner: Cancelled(NoSuitableSlot) or Cancelled(NoShow)
            Assert.True(
                loaded.CancelReason == CancelReason.NoSuitableSlot ^
                loaded.CancelReason == CancelReason.NoShow,
                $"Unexpected cancel reason: {loaded.CancelReason}");

            // IsNoShow matches the winner
            if (loaded.CancelReason == CancelReason.NoShow)
            {
                Assert.True(loaded.IsNoShow);
                Assert.Equal(PrepaidOutcome.Forfeited, loaded.PrepaidOutcome);
            }
            else
            {
                Assert.False(loaded.IsNoShow);
                Assert.Equal(PrepaidOutcome.Refunded, loaded.PrepaidOutcome);
            }

            // At most 1 refund call across both paths
            lock (paymentClient.RefundCalls)
            {
                Assert.True(paymentClient.RefundCalls.Count <= 1);
                if (loaded.CancelReason == CancelReason.NoSuitableSlot)
                {
                    Assert.Single(paymentClient.RefundCalls);
                }
                else
                {
                    Assert.Empty(paymentClient.RefundCalls);
                }
            }

            // Exactly 1 cancel event in outbox
            var cancelEvents = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == resId && m.EventType == "booking.reservation-cancelled.v1")
                .ToListAsync(cancellationToken);
            Assert.Single(cancelEvents);
        }
    }

    // -------------------------------------------------------------------------
    // S-12: Customer cancel race against real PostgreSQL
    // -------------------------------------------------------------------------
    [Fact]
    public async Task S12_CustomerCancelRace_HandledGracefullyViaConcurrencyToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(apptTime);
        var paymentClient = new TrackingPaymentClient();

        await using var provider = CreateProvider(clock, paymentClient);
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var resId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateConfirmedReservation(resId, lotId, apptTime, 50000m, confirmedAt: apptTime.AddMinutes(-2));
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        var barrier = new Barrier(2);

        async Task RunCustomerCancelAsync()
        {
            try
            {
                await using var scope = provider.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();
                barrier.SignalAndWait(cancellationToken);
                await handler.Handle(new CancelReservationCommand(resId, UserId, "Customer cancelled concurrently"), cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.InnerException?.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Expected concurrency conflict
            }
        }

        async Task RunApiCancelAsync()
        {
            try
            {
                await using var scope = provider.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<CancelForNoSuitableSlotCommandHandler>();
                barrier.SignalAndWait(cancellationToken);
                await handler.Handle(new CancelForNoSuitableSlotCommand(lotId, PlateNumber, apptTime), cancellationToken);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.InnerException?.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Expected concurrency conflict
            }
        }

        var customerTask = Task.Run(RunCustomerCancelAsync, cancellationToken);
        var apiTask = Task.Run(RunApiCancelAsync, cancellationToken);

        await Task.WhenAll(customerTask, apiTask);

        // Verify database state: exactly one winner
        await using (var assertScope = provider.CreateAsyncScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resId, cancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(ReservationStatus.Cancelled, loaded.Status);

            Assert.True(
                loaded.CancelReason == CancelReason.CustomerCancelled ^
                loaded.CancelReason == CancelReason.NoSuitableSlot,
                $"Unexpected cancel reason: {loaded.CancelReason}");

            Assert.False(loaded.IsNoShow);

            // At most 1 refund call
            lock (paymentClient.RefundCalls)
            {
                Assert.True(paymentClient.RefundCalls.Count <= 1);
            }

            // Exactly 1 cancel event in outbox
            var cancelEvents = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == resId && m.EventType == "booking.reservation-cancelled.v1")
                .ToListAsync(cancellationToken);
            Assert.Single(cancelEvents);
        }
    }

    // -------------------------------------------------------------------------
    // S-13: Held Quota Drop against real PostgreSQL
    // -------------------------------------------------------------------------
    [Fact]
    public async Task S13_HeldQuotaDrop_DecreasesImmediatelyWhenCancelledNoSuitableSlot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(apptTime);
        var paymentClient = new TrackingPaymentClient();

        await using var provider = CreateProvider(clock, paymentClient);
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var resId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateConfirmedReservation(resId, lotId, apptTime, 50000m);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Check held quota before cancel
        await using (var checkScope = provider.CreateAsyncScope())
        {
            var quotaReader = checkScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var countBefore = await quotaReader.CountAsync(lotId, apptTime, cancellationToken);
            Assert.Equal(1, countBefore);
        }

        // Cancel for no suitable slot
        await using (var actScope = provider.CreateAsyncScope())
        {
            var handler = actScope.ServiceProvider.GetRequiredService<CancelForNoSuitableSlotCommandHandler>();
            var command = new CancelForNoSuitableSlotCommand(lotId, PlateNumber, apptTime);
            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.Cancelled);
        }

        // Check held quota after cancel: dropped immediately to 0
        await using (var assertScope = provider.CreateAsyncScope())
        {
            var quotaReader = assertScope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            var countAfter = await quotaReader.CountAsync(lotId, apptTime, cancellationToken);
            Assert.Equal(0, countAfter);
        }
    }

    // -------------------------------------------------------------------------
    // S-14: Outbox Atomicity against real PostgreSQL
    // -------------------------------------------------------------------------
    [Fact]
    public async Task S14_OutboxAtomicity_OutboxEventPersistedInSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(apptTime);
        var paymentClient = new TrackingPaymentClient();

        await using var provider = CreateProvider(clock, paymentClient);
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var resId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateConfirmedReservation(resId, lotId, apptTime, 50000m);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Execute CancelForNoSuitableSlotCommand
        await using (var actScope = provider.CreateAsyncScope())
        {
            var handler = actScope.ServiceProvider.GetRequiredService<CancelForNoSuitableSlotCommandHandler>();
            var command = new CancelForNoSuitableSlotCommand(lotId, PlateNumber, apptTime);
            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.Cancelled);
        }

        // Verify outbox message in PostgreSQL
        await using (var assertScope = provider.CreateAsyncScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var outboxMsg = await db.OutboxMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.CorrelationId == resId && m.EventType == "booking.reservation-cancelled.v1", cancellationToken);

            Assert.NotNull(outboxMsg);
            Assert.Contains("NoSuitableSlot", outboxMsg.Payload);
            Assert.Contains("50000", outboxMsg.Payload);
        }
    }

    // -------------------------------------------------------------------------
    // S-19: Real PostgreSQL Slot Status Handler
    // -------------------------------------------------------------------------
    [Fact]
    public async Task S19_HandleSlotStatusChanged_CancelsConfirmedReservationInPostgreSql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lotId = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(apptTime);
        var paymentClient = new TrackingPaymentClient();

        await using var provider = CreateProvider(clock, paymentClient);
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var db = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var resId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateConfirmedReservation(resId, lotId, apptTime, 50000m);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Slot transitions to Maintenance
        await using (var actScope = provider.CreateAsyncScope())
        {
            var handler = actScope.ServiceProvider.GetRequiredService<HandleSlotStatusChangedCommandHandler>();
            var command = new HandleSlotStatusChangedCommand(
                lotId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "A-01",
                "Maintenance",
                "Available",
                "Sensor",
                apptTime,
                1);

            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value!.CancelledReservationsCount);
            Assert.Contains("Tiền đã được hoàn về ví", result.Value.Message);
        }

        // Verify in PostgreSQL: Cancelled with NoSuitableSlot, Refunded
        await using (var assertScope = provider.CreateAsyncScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resId, cancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(ReservationStatus.Cancelled, loaded.Status);
            Assert.Equal(CancelReason.NoSuitableSlot, loaded.CancelReason);
            Assert.Equal(PrepaidOutcome.Refunded, loaded.PrepaidOutcome);

            // Verify Outbox
            var outboxMsg = await db.OutboxMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.CorrelationId == resId && m.EventType == "booking.reservation-cancelled.v1", cancellationToken);
            Assert.NotNull(outboxMsg);
        }

        // Verify Payment Refund call
        var call = Assert.Single(paymentClient.RefundCalls);
        Assert.Equal(resId, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);

        // --- Part 2: Payment FAILS -> never Refunded without a real refund (PrepaidOutcome remains null) ---
        var res2Id = Guid.NewGuid();
        await using (var seedScope2 = provider.CreateAsyncScope())
        {
            var db = seedScope2.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res2 = CreateConfirmedReservation(res2Id, lotId, apptTime, 50000m, plate: "51B-999.88");
            db.Reservations.Add(res2);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Configure payment to fail
        paymentClient.RefundCalls.Clear();
        paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        await using (var actScope2 = provider.CreateAsyncScope())
        {
            var handler = actScope2.ServiceProvider.GetRequiredService<HandleSlotStatusChangedCommandHandler>();
            var command = new HandleSlotStatusChangedCommand(
                lotId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "A-02",
                "Maintenance",
                "Available",
                "Sensor",
                apptTime,
                2);

            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value!.CancelledReservationsCount);
            Assert.Contains("Tiền hoàn đang được xử lý", result.Value.Message);
        }

        // Verify in PostgreSQL: Status is Cancelled, but PrepaidOutcome is strictly NULL (never marked Refunded!)
        await using (var assertScope2 = provider.CreateAsyncScope())
        {
            var db = assertScope2.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded2 = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == res2Id, cancellationToken);
            Assert.NotNull(loaded2);
            Assert.Equal(ReservationStatus.Cancelled, loaded2.Status);
            Assert.Equal(CancelReason.NoSuitableSlot, loaded2.CancelReason);
            Assert.Null(loaded2.PrepaidOutcome); // NEVER Refunded without real refund!
        }
    }
}
