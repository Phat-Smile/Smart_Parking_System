using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingSystem.Booking.Application;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

[Collection("CapacityGuardNonParallelCollection")]
public sealed class ScheduledBookingPostgreSqlIntegrationTests(PostgreSqlContainerFixture postgres)
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = BaseTime;
    }

    private sealed class TestVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(string plateNumber, Guid userId, CancellationToken cancellationToken = default)
        {
            var canonical = LicensePlateNormalizer.Normalize(plateNumber);
            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(userId, Guid.NewGuid(), canonical, SupportedVehicleType.Car, true)));
        }
    }

    private sealed class TestParkingLotCapacityProvider : IParkingLotCapacityProvider
    {
        public int ValidCapacity { get; set; } = 10;
        public int RatioPercent { get; set; } = 100;
        public int AvailableSlots { get; set; } = 10;

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotCapacityInfo>.Success(
                new ParkingLotCapacityInfo(ValidCapacity, RatioPercent, AvailableSlots, ReservationEnabled: true, DepositAmount: 0m)));
    }

    private sealed class TestParkingPricingProvider : IParkingPricingProvider
    {
        public decimal Price { get; set; } = 20000m;
        public string BillingMode { get; set; } = "Block";
        public int? BlockMinutes { get; set; } = 60;
        public decimal OvernightFee { get; set; } = 10000m;
        public TimeSpan? OvernightTimeMark { get; set; } = TimeSpan.Zero;
        public int? PolicyVersion { get; set; } = 3;

        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingPricingInfo>.Success(
                new ParkingPricingInfo(lotId, vehicleGroup, Price, BillingMode, BlockMinutes, OvernightFee, "VND", OvernightTimeMark, PolicyVersion)));
    }

    private (ServiceProvider Provider, TestClock Clock, TestParkingLotCapacityProvider CapacityProvider, TestParkingPricingProvider PricingProvider) CreateTestEnvironment()
    {
        var builder = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            MaxPoolSize = 100
        };
        var connStr = builder.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(connStr))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddApplicationServices();
        services.AddLogging(logging => logging.AddProvider(NullLoggerProvider.Instance));

        var testClock = new TestClock();
        var capacityProvider = new TestParkingLotCapacityProvider();
        var pricingProvider = new TestParkingPricingProvider();

        services.AddSingleton<IClock>(testClock);
        services.AddSingleton<IVehicleOwnershipClient>(new TestVehicleOwnershipClient());
        services.AddSingleton<IParkingLotCapacityProvider>(capacityProvider);
        services.AddSingleton<IParkingPricingProvider>(pricingProvider);
        services.AddSingleton(new BookingCapacityOptions
        {
            LeadTimeMinutes = 30,
            GracePeriodMinutes = 15,
            TempHoldDurationMinutes = 10,
            LockTimeoutSeconds = 10,
            MaxActiveReservationsPerUser = 3
        });

        var provider = services.BuildServiceProvider(validateScopes: true);
        return (provider, testClock, capacityProvider, pricingProvider);
    }

    private static readonly SemaphoreSlim TestExecutionLock = new(1, 1);
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _isMigrated;

    private static async Task EnsureMigratedAsync(ServiceProvider provider)
    {
        if (_isMigrated) return;

        await MigrationLock.WaitAsync();
        try
        {
            if (_isMigrated) return;
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync();
            _isMigrated = true;
        }
        finally
        {
            MigrationLock.Release();
        }
    }

    // =========================================================================
    // 1. Scheduled hold persists PrepaidAmount and jsonb snapshot that can be read back
    // =========================================================================
    [Fact]
    public async Task Scheduled_Hold_Persists_PrepaidAmount_And_Jsonb_Snapshot_Read_Back()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var lotId = Guid.NewGuid();
            var resId = Guid.NewGuid();
            var appTime = clock.UtcNow.AddHours(3);

            var cmd = new CreateTemporaryHoldCommand(
                ReservationId: resId,
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51F-123.45",
                LotId: lotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: appTime,
                DurationMinutes: 120,
                PrepaidAmount: 999999m); // client amount ignored

            var result = await mediator.Send(cmd);
            Assert.True(result.IsSuccess);

            // Read directly back from DB
            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var saved = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resId);

            Assert.NotNull(saved);
            Assert.Equal(40000m, saved.PrepaidAmount); // 2 hours = 2 blocks * 20,000 = 40,000
            Assert.NotNull(saved.PriceSnapshot);

            using var doc = JsonDocument.Parse(saved.PriceSnapshot!);
            Assert.Equal(40000m, doc.RootElement.GetProperty("calculatedTotal").GetDecimal());
            Assert.Equal("Block", doc.RootElement.GetProperty("billingMode").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("policyVersion").GetInt32());
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // 2. Ceiling reached for an overlapping scheduled window -> CapacityExceeded; non-overlapping window succeeds
    // =========================================================================
    [Fact]
    public async Task Ceiling_Reached_For_Overlapping_Window_Fails_And_Non_Overlapping_Succeeds()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, capacityProvider, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Capacity 1, 100% ratio -> ceiling = 1
            capacityProvider.ValidCapacity = 1;
            capacityProvider.RatioPercent = 100;
            capacityProvider.AvailableSlots = 1;

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // Hold 1: 14:00 to 16:00 (window [13:30, 16:00))
            var cmd1 = new CreateTemporaryHoldCommand(
                ReservationId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51F-111.11",
                LotId: lotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: clock.UtcNow.AddHours(4),
                DurationMinutes: 120,
                PrepaidAmount: 0m);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // Hold 2: Overlapping window (15:00 to 17:00, window [14:30, 17:00)) -> REJECTED CapacityExceeded
            var cmd2 = new CreateTemporaryHoldCommand(
                ReservationId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51F-222.22",
                LotId: lotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: clock.UtcNow.AddHours(5),
                DurationMinutes: 120,
                PrepaidAmount: 0m);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, res2.Error.Code);

            // Hold 3: Non-overlapping window (17:00 to 19:00, window [16:30, 19:00)) -> SUCCEEDS
            var cmd3 = new CreateTemporaryHoldCommand(
                ReservationId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51F-333.33",
                LotId: lotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: clock.UtcNow.AddHours(7),
                DurationMinutes: 120,
                PrepaidAmount: 0m);

            var res3 = await mediator.Send(cmd3);
            Assert.True(res3.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // 3. 20 concurrent scheduled requests for the last slot -> exactly 1 success
    // =========================================================================
    [Fact]
    public async Task Twenty_Concurrent_Scheduled_Requests_For_Last_Slot_Allows_Exactly_One_Success()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, capacityProvider, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Capacity 1, 100% ratio -> ceiling = 1
            capacityProvider.ValidCapacity = 1;
            capacityProvider.RatioPercent = 100;
            capacityProvider.AvailableSlots = 1;

            const int totalConcurrent = 20;
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, totalConcurrent).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: $"51F-CONC-{i:D2}",
                    LotId: lotId,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: ReservationType.Scheduled,
                    AppointmentTime: clock.UtcNow.AddHours(3),
                    DurationMinutes: 60,
                    PrepaidAmount: 0m);

                return await mediator.Send(cmd);
            }).ToList();

            barrier.SetResult();
            var results = await Task.WhenAll(tasks);

            var successes = results.Where(r => r.IsSuccess).ToList();
            var failures = results.Where(r => !r.IsSuccess).ToList();

            Assert.Single(successes);
            Assert.Equal(totalConcurrent - 1, failures.Count);
            Assert.All(failures, f => Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, f.Error.Code));
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // 4. Expired temporary hold no longer blocks a new scheduled hold
    // =========================================================================
    [Fact]
    public async Task Expired_Temporary_Hold_No_Longer_Blocks_New_Scheduled_Hold()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, capacityProvider, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            capacityProvider.ValidCapacity = 1;
            capacityProvider.RatioPercent = 100;
            capacityProvider.AvailableSlots = 1;

            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                // Expired hold: TempHoldExpiresAt in the past
                var expiredHold = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-EXPIRED", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddHours(3), 20000m, clock.UtcNow.AddMinutes(-30),
                    durationMinutes: 60,
                    tempHoldExpiresAt: clock.UtcNow.AddMinutes(-20),
                    windowStartAt: clock.UtcNow.AddHours(2).AddMinutes(30),
                    windowEndAt: clock.UtcNow.AddHours(4),
                    holdEndAt: clock.UtcNow.AddHours(3).AddMinutes(15));
                expiredHold.Expire();
                db.Reservations.Add(expiredHold);
                await db.SaveChangesAsync();
            }

            // New scheduled hold in the exact same window should succeed
            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var cmd = new CreateTemporaryHoldCommand(
                ReservationId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51F-NEWONE",
                LotId: lotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: clock.UtcNow.AddHours(3),
                DurationMinutes: 60,
                PrepaidAmount: 0m);

            var result = await mediator.Send(cmd);
            Assert.True(result.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // 5. Condition (ii): Confirmed Scheduled booking past its HoldEndAt is not counted; inside it is counted
    // =========================================================================
    [Fact]
    public async Task Condition_ii_Confirmed_Scheduled_Past_HoldEndAt_Not_Counted_Inside_Is_Counted()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, capacityProvider, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            capacityProvider.ValidCapacity = 10;
            capacityProvider.RatioPercent = 100;
            // Available slot count = 1!
            capacityProvider.AvailableSlots = 1;

            // Seed confirmed reservation whose window has started (WindowStartAt <= now)
            // Appointment: Now + 10m. Lead 30m -> WindowStartAt = Now - 20m (already started!)
            // HoldEndAt: Now + 25m.
            var resId = Guid.NewGuid();
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var confirmedRes = Reservation.CreateTemporaryHold(
                    resId, Guid.NewGuid(), Guid.NewGuid(), "51F-CONFIRMED", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddMinutes(10), 20000m, clock.UtcNow.AddMinutes(-20),
                    durationMinutes: 120,
                    tempHoldExpiresAt: clock.UtcNow.AddMinutes(-10),
                    windowStartAt: clock.UtcNow.AddMinutes(-20),
                    windowEndAt: clock.UtcNow.AddMinutes(130),
                    holdEndAt: clock.UtcNow.AddMinutes(25));
                confirmedRes.Confirm(clock.UtcNow.AddMinutes(-15));
                db.Reservations.Add(confirmedRes);
                await db.SaveChangesAsync();
            }

            // Test 1: While inside HoldEndAt (HoldEndAt = Now + 25m > Now)
            // Active holds count against AvailableSlotCount (1 >= 1) -> NoAvailableSlots!
            await using (var scope1 = provider.CreateAsyncScope())
            {
                var mediator = scope1.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                var cmdInside = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: "51F-TRYINSIDE",
                    LotId: lotId,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: ReservationType.Scheduled,
                    AppointmentTime: clock.UtcNow.AddMinutes(10), // window has also started
                    DurationMinutes: 60,
                    PrepaidAmount: 0m);

                var resultInside = await mediator.Send(cmdInside);
                Assert.False(resultInside.IsSuccess);
                Assert.Equal(ReservationCapacityErrors.NoAvailableSlots.Code, resultInside.Error.Code);
            }

            // Test 2: Advance clock past HoldEndAt (advance by 30 minutes, now is clock.UtcNow + 30m)
            // Now is past HoldEndAt (Now > Now - 20m + 45m = Now + 25m), but still inside WindowEndAt (Now + 130m)
            clock.UtcNow = clock.UtcNow.AddMinutes(30);

            await using (var scope2 = provider.CreateAsyncScope())
            {
                var mediator = scope2.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                var cmdPast = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: "51F-TRYPAST",
                    LotId: lotId,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: ReservationType.Scheduled,
                    AppointmentTime: clock.UtcNow.AddMinutes(10),
                    DurationMinutes: 60,
                    PrepaidAmount: 0m);

                var resultPast = await mediator.Send(cmdPast);
                // B8 fix verified: with HoldEndAt > now, past confirmed reservation is no longer counted against AvailableSlots!
                Assert.True(resultPast.IsSuccess);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }
}

