extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.Testing;
using Xunit;

using BookingArrivalEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ArrivalHeldQuotaEndpoints;
using BookingArrivalResponse = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ArrivalHeldQuotaResponse;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ArrivalHeldQuotaIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string ExpectedArrivalKey = "test_arrival_secret_key_12345";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Counts_only_currently_active_car_reservations_overlapping_future_arrival()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var config = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var now = DateTimeOffset.UtcNow;
        var arrivalAt = now.AddHours(1);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var confirmed = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            confirmed.Confirm(now);
            var temporaryHold = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            var expiredHold = Create(lotId, now, arrivalAt, now.AddMinutes(-10));
            var outsideWindow = Create(lotId, now, arrivalAt.AddHours(3), now.AddMinutes(10));
            outsideWindow.Confirm(now);
            var used = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            used.Confirm(now);
            used.Use(now);
            db.Reservations.AddRange(confirmed, temporaryHold, expiredHold, outsideWindow, used);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            Assert.Equal(2, await reader.CountAsync(lotId, arrivalAt, cancellationToken));
            Assert.Equal(0, await reader.CountAsync(Guid.NewGuid(), arrivalAt, cancellationToken));
        }
    }

    [Fact]
    public async Task J_05_HeldQuota_CountedAt_A_Plus_15M59S_NotCountedAt_A_Plus_16M00S()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var config = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            // Create using default windows (holdEndAt defaults to A + 16:00)
            var res = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "30A" + Guid.NewGuid().ToString("N")[..8],
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: appointmentTime,
                prepaidAmount: 50000m,
                createdAt: now,
                durationMinutes: 120,
                tempHoldExpiresAt: now.AddMinutes(10));
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // At A + 15:59 -> inside [WindowStartAt, HoldEndAt) -> count = 1
            var at15M59S = appointmentTime.AddMinutes(15).AddSeconds(59);
            var countAt15M59S = await reader.CountAsync(lotId, at15M59S, cancellationToken);
            Assert.Equal(1, countAt15M59S);

            // At A + 16:00 -> at HoldEndAt boundary -> half-open -> count = 0
            var at16M00S = appointmentTime.AddMinutes(16);
            var countAt16M00S = await reader.CountAsync(lotId, at16M00S, cancellationToken);
            Assert.Equal(0, countAt16M00S);
        }
    }

    // =========================================================================
    // US-RES-009 Required Tests (Q-01 .. Q-10)
    // =========================================================================

    [Fact]
    public async Task Q_01_Confirmed_HWD_Boundaries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: appointmentTime,
                prepaidAmount: 20000m,
                createdAt: now,
                tempHoldExpiresAt: now.AddMinutes(10));
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // t = A - 30:01 (09:29:59) -> before WindowStartAt -> 0
            var beforeStart = appointmentTime.AddMinutes(-30).AddSeconds(-1);
            Assert.Equal(0, await reader.CountAsync(lotId, beforeStart, ct));

            // t = A - 30:00 (09:30:00) -> WindowStartAt boundary (inclusive) -> 1
            var atStart = appointmentTime.AddMinutes(-30);
            Assert.Equal(1, await reader.CountAsync(lotId, atStart, ct));

            // t = A + 15:59 (10:15:59) -> before HoldEndAt -> 1
            var beforeEnd = appointmentTime.AddMinutes(15).AddSeconds(59);
            Assert.Equal(1, await reader.CountAsync(lotId, beforeEnd, ct));

            // t = A + 16:00 (10:16:00) -> HoldEndAt boundary (exclusive) -> 0
            var atEnd = appointmentTime.AddMinutes(16);
            Assert.Equal(0, await reader.CountAsync(lotId, atEnd, ct));
        }
    }

    [Fact]
    public async Task Q_02_TemporaryHold_NotExpired_Counted_Expired_NotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // 1. Unexpired TemporaryHold (TempHoldExpiresAt > now) -> counted
            var unexpired = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51B" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: appointmentTime,
                prepaidAmount: 50000m,
                createdAt: now,
                durationMinutes: 120,
                tempHoldExpiresAt: now.AddMinutes(10));

            // 2. Expired TemporaryHold (TempHoldExpiresAt <= now) -> not counted
            var expired = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51C" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: appointmentTime,
                prepaidAmount: 50000m,
                createdAt: now.AddMinutes(-20),
                durationMinutes: 120,
                tempHoldExpiresAt: now.AddMinutes(-5));

            db.Reservations.AddRange(unexpired, expired);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            Assert.Equal(1, await reader.CountAsync(lotId, appointmentTime, ct));
        }
    }

    [Fact]
    public async Task Q_03_Used_Cancelled_EachReason_And_Expired_NotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // 1. Used
            var used = CreateReservation(lotId, now, appointmentTime);
            used.Confirm(now);
            used.Use(now);

            // 2. Cancelled: CustomerCancelled
            var customerCancelled = CreateReservation(lotId, now, appointmentTime);
            customerCancelled.Confirm(now);
            customerCancelled.Cancel(now, CancelReason.CustomerCancelled, PrepaidOutcome.Refunded);

            // 3. Cancelled: PaymentFailed
            var paymentFailed = CreateReservation(lotId, now, appointmentTime);
            paymentFailed.Cancel(now, CancelReason.PaymentFailed, null);

            // 4. Cancelled: NoSuitableSlot
            var noSuitableSlot = CreateReservation(lotId, now, appointmentTime);
            noSuitableSlot.Confirm(now);
            noSuitableSlot.Cancel(now, CancelReason.NoSuitableSlot, PrepaidOutcome.Refunded);

            // 5. Cancelled: NoShow
            var noShow = CreateReservation(lotId, now, appointmentTime);
            noShow.Confirm(now);
            noShow.Cancel(appointmentTime.AddMinutes(16), CancelReason.NoShow, PrepaidOutcome.Forfeited);

            // 6. Expired
            var expired = CreateReservation(lotId, now, appointmentTime);
            expired.Expire();

            db.Reservations.AddRange(used, customerCancelled, paymentFailed, noSuitableSlot, noShow, expired);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            Assert.Equal(0, await reader.CountAsync(lotId, appointmentTime, ct));
        }
    }

    [Fact]
    public async Task Q_04_Scheduled_InsideDuration_PastHoldEndAt_NotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // Scheduled with 3h duration (180m):
            // Hold window: [A - 30m, A + 16m)
            // Ceiling window: [A - 30m, A + 180m)
            var scheduled = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51D" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: appointmentTime,
                prepaidAmount: 60000m,
                createdAt: now,
                durationMinutes: 180,
                tempHoldExpiresAt: now.AddMinutes(10));
            scheduled.Confirm(now);
            db.Reservations.Add(scheduled);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // At t = A + 10m (10:10): inside hold window [A - 30m, A + 16m) -> 1
            var insideHold = appointmentTime.AddMinutes(10);
            Assert.Equal(1, await reader.CountAsync(lotId, insideHold, ct));

            // At t = A + 120m (12:00): inside booked 3h duration, but past HoldEndAt (A + 16m) -> 0
            var insideDurationAfterHoldEnd = appointmentTime.AddMinutes(120);
            Assert.Equal(0, await reader.CountAsync(lotId, insideDurationAfterHoldEnd, ct));
        }
    }

    [Fact]
    public async Task Q_05_Scheduled_Tomorrow_NotCounted_Today()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var tomorrowAppointment = now.AddDays(1).AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateReservation(lotId, now, tomorrowAppointment);
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // Query at today 2 hours from now -> tomorrow's reservation must not reduce today's walk-in places -> 0
            var todayEvaluation = now.AddHours(2);
            Assert.Equal(0, await reader.CountAsync(lotId, todayEvaluation, ct));
        }
    }

    [Fact]
    public async Task Q_06_Reservation_AnotherLot_NotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotIdA = Guid.NewGuid();
        var lotIdB = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var resA = CreateReservation(lotIdA, now, appointmentTime);
            resA.Confirm(now);
            db.Reservations.Add(resA);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // Querying Lot B must not see Lot A's reservation -> 0
            Assert.Equal(0, await reader.CountAsync(lotIdB, appointmentTime, ct));

            // Querying Lot A sees its reservation -> 1
            Assert.Equal(1, await reader.CountAsync(lotIdA, appointmentTime, ct));
        }
    }

    [Fact]
    public async Task Q_07_ThreeReservations_InsideWindow_PlusOne_OutsideWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateDbProviderAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var t = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // Res 1: HWD, A = t, Confirmed -> window [t - 30m, t + 16m) -> contains t
            var res1 = Reservation.CreateTemporaryHold(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "51G11111", lotId, VehicleGroup.Car, ReservationType.HoldWhileDriving,
                t, 20000m, now, tempHoldExpiresAt: now.AddMinutes(10));
            res1.Confirm(now);

            // Res 2: Scheduled, A = t - 5m, Confirmed -> window [t - 35m, t + 11m) -> contains t
            var res2 = Reservation.CreateTemporaryHold(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "51G22222", lotId, VehicleGroup.Car, ReservationType.Scheduled,
                t.AddMinutes(-5), 50000m, now, durationMinutes: 120, tempHoldExpiresAt: now.AddMinutes(10));
            res2.Confirm(now);

            // Res 3: Scheduled, A = t + 10m, TemporaryHold unexpired -> window [t - 20m, t + 26m) -> contains t
            var res3 = Reservation.CreateTemporaryHold(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "51G33333", lotId, VehicleGroup.Car, ReservationType.Scheduled,
                t.AddMinutes(10), 50000m, now, durationMinutes: 120, tempHoldExpiresAt: now.AddMinutes(10));

            // Res 4: Scheduled, A = t + 5h (far outside window)
            var res4 = Reservation.CreateTemporaryHold(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "51G44444", lotId, VehicleGroup.Car, ReservationType.Scheduled,
                t.AddHours(5), 50000m, now, durationMinutes: 120, tempHoldExpiresAt: now.AddMinutes(10));
            res4.Confirm(now);

            db.Reservations.AddRange(res1, res2, res3, res4);
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            Assert.Equal(3, await reader.CountAsync(lotId, t, ct));
        }
    }

    [Fact]
    public async Task Q_08_VehicleGroup_Motorbike_Zero_Missing_SameAsCar_Truck_BadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ExpectedArrivalKey, ct);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

        var lotId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var arrivalAt = now.AddHours(2);

        // Seed 1 Confirmed Car reservation
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateReservation(lotId, now, arrivalAt);
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        // 1. vehicleGroup=Motorbike -> 200 OK, heldQuota = 0 (no DB query)
        var responseMotorbike = await client.GetAsync(
            $"/internal/booking/held-quota?lotId={lotId}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}&vehicleGroup=Motorbike",
            ct);
        Assert.Equal(HttpStatusCode.OK, responseMotorbike.StatusCode);
        var jsonMotorbike = await responseMotorbike.Content.ReadFromJsonAsync<BookingArrivalResponse>(JsonOptions, ct);
        Assert.NotNull(jsonMotorbike);
        Assert.Equal(lotId, jsonMotorbike.LotId);
        Assert.Equal("Motorbike", jsonMotorbike.VehicleGroup);
        Assert.Equal(0, jsonMotorbike.HeldQuota);

        // 2. vehicleGroup missing -> defaults to Car -> returns heldQuota = 1
        var responseMissing = await client.GetAsync(
            $"/internal/booking/held-quota?lotId={lotId}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}",
            ct);
        Assert.Equal(HttpStatusCode.OK, responseMissing.StatusCode);
        var jsonMissing = await responseMissing.Content.ReadFromJsonAsync<BookingArrivalResponse>(JsonOptions, ct);
        Assert.NotNull(jsonMissing);
        Assert.Equal(lotId, jsonMissing.LotId);
        Assert.Equal("Car", jsonMissing.VehicleGroup);
        Assert.Equal(1, jsonMissing.HeldQuota);

        // 3. vehicleGroup=Truck -> 400 Bad Request
        var responseTruck = await client.GetAsync(
            $"/internal/booking/held-quota?lotId={lotId}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}&vehicleGroup=Truck",
            ct);
        Assert.Equal(HttpStatusCode.BadRequest, responseTruck.StatusCode);
    }

    [Fact]
    public async Task Q_09_Http_Endpoint_Authentication_Validation_And_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ExpectedArrivalKey, ct);
        var client = app.GetTestClient();

        var lotId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var arrivalAt = now.AddHours(2);

        // Seed 1 Confirmed Car reservation
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var res = CreateReservation(lotId, now, arrivalAt);
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(ct);
        }

        var validUrl = $"/internal/booking/held-quota?lotId={lotId}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}&vehicleGroup=Car";

        // Case 1: Missing key header -> 403 Forbidden
        var missingKeyResponse = await client.GetAsync(validUrl, ct);
        Assert.Equal(HttpStatusCode.Forbidden, missingKeyResponse.StatusCode);

        // Case 2: Wrong key header -> 403 Forbidden
        using var wrongKeyRequest = new HttpRequestMessage(HttpMethod.Get, validUrl);
        wrongKeyRequest.Headers.Add("X-Arrival-Service-Key", "invalid_secret_key");
        var wrongKeyResponse = await client.SendAsync(wrongKeyRequest, ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongKeyResponse.StatusCode);

        // Case 3: Key not configured on server -> 503 Service Unavailable
        await using (var unconfiguredApp = await CreateAppAsync(arrivalKeyConfig: null, ct))
        {
            var unconfiguredClient = unconfiguredApp.GetTestClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, validUrl);
            req.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);
            var response503 = await unconfiguredClient.SendAsync(req, ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response503.StatusCode);
        }

        // Case 4: LotId empty (Guid.Empty) -> 400 Bad Request
        using var emptyLotRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/booking/held-quota?lotId={Guid.Empty}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}&vehicleGroup=Car");
        emptyLotRequest.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);
        var emptyLotResponse = await client.SendAsync(emptyLotRequest, ct);
        Assert.Equal(HttpStatusCode.BadRequest, emptyLotResponse.StatusCode);

        // Case 5: Correct call -> 200 OK with lotId, arrivalAt, vehicleGroup, heldQuota
        using var validRequest = new HttpRequestMessage(HttpMethod.Get, validUrl);
        validRequest.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);
        var validResponse = await client.SendAsync(validRequest, ct);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);

        var body = await validResponse.Content.ReadFromJsonAsync<BookingArrivalResponse>(JsonOptions, ct);
        Assert.NotNull(body);
        Assert.Equal(lotId, body.LotId);
        Assert.Equal(arrivalAt, body.ArrivalAt);
        Assert.Equal("Car", body.VehicleGroup);
        Assert.Equal(1, body.HeldQuota);
    }

    [Fact]
    public void Q_10_WalkIn_Places_Formula_Example_Demonstration()
    {
        // 1. Available = 5, held = 3 -> walk-in places 2, "Còn chỗ"
        var (places1, msg1) = WalkInPlacesCalculator.Calculate(availableSlots: 5, heldQuota: 3);
        Assert.Equal(2, places1);
        Assert.Equal(WalkInPlacesCalculator.AvailableMessage, msg1);

        // 2. Available = 5, held = 5 -> walk-in places 0, "Hết chỗ cho xe không đặt trước"
        var (places2, msg2) = WalkInPlacesCalculator.Calculate(availableSlots: 5, heldQuota: 5);
        Assert.Equal(0, places2);
        Assert.Equal(WalkInPlacesCalculator.NoPlacesForUnreservedMessage, msg2);

        // 3. Available = 5, held = 7 -> walk-in places 0 (clamped by max(0, ...)), "Hết chỗ cho xe không đặt trước"
        var (places3, msg3) = WalkInPlacesCalculator.Calculate(availableSlots: 5, heldQuota: 7);
        Assert.Equal(0, places3);
        Assert.Equal(WalkInPlacesCalculator.NoPlacesForUnreservedMessage, msg3);

        // 4. Available = 0, held = 0 (or any) -> walk-in places 0, "Đã đầy"
        var (places4, msg4) = WalkInPlacesCalculator.Calculate(availableSlots: 0, heldQuota: 0);
        Assert.Equal(0, places4);
        Assert.Equal(WalkInPlacesCalculator.FullMessage, msg4);

        var (places5, msg5) = WalkInPlacesCalculator.Calculate(availableSlots: 0, heldQuota: 3);
        Assert.Equal(0, places5);
        Assert.Equal(WalkInPlacesCalculator.FullMessage, msg5);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<ServiceProvider> CreateDbProviderAsync(CancellationToken cancellationToken)
    {
        var config = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);
        services.AddLogging();
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        return provider;
    }

    private async Task<WebApplication> CreateAppAsync(string? arrivalKeyConfig, CancellationToken cancellationToken)
    {
        var inMemoryConfig = new Dictionary<string, string?>();
        if (arrivalKeyConfig is not null)
        {
            inMemoryConfig["ArrivalIntegration:Key"] = arrivalKeyConfig;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(inMemoryConfig);
        builder.Configuration.AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Services.AddPersistence(builder.Configuration);

        var app = builder.Build();
        app.UseRouting();
        BookingArrivalEndpoints.MapArrivalHeldQuotaEndpoints(app);

        await app.StartAsync(cancellationToken);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        return app;
    }

    private static Reservation CreateReservation(Guid lotId, DateTimeOffset now, DateTimeOffset appointmentTime)
    {
        return Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
            lotId: lotId,
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: appointmentTime,
            prepaidAmount: 50000m,
            createdAt: now,
            durationMinutes: 120,
            tempHoldExpiresAt: now.AddMinutes(10));
    }

    private static Reservation Create(Guid lotId, DateTimeOffset now,
        DateTimeOffset at, DateTimeOffset holdExpiry)
    {
        return Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(), userId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            plateNumber: "30A" + Guid.NewGuid().ToString("N")[..8],
            lotId: lotId, vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: at, prepaidAmount: 0, createdAt: now,
            durationMinutes: 60, tempHoldExpiresAt: holdExpiry,
            windowStartAt: at.AddMinutes(-30), windowEndAt: at.AddHours(1),
            holdEndAt: at.AddHours(1));
    }
}
