using System.Collections.Concurrent;
using System.Data;
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
using ParkingSystem.Booking.Persistence.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

[CollectionDefinition("CapacityGuardNonParallelCollection", DisableParallelization = true)]
public class CapacityGuardNonParallelCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
}

[Collection("CapacityGuardNonParallelCollection")]
public sealed class CapacityGuardIntegrationTests(PostgreSqlContainerFixture postgres)
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

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
        private readonly ConcurrentDictionary<Guid, ParkingLotCapacityInfo> _lotCapacity = new();

        public void SetLotCapacity(Guid lotId, int validCapacity, int ratioPercent, int availableSlots)
        {
            _lotCapacity[lotId] = new ParkingLotCapacityInfo(validCapacity, ratioPercent, availableSlots, ReservationEnabled: true, DepositAmount: 0m);
        }

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            if (_lotCapacity.TryGetValue(lotId, out var info))
            {
                return Task.FromResult(Result<ParkingLotCapacityInfo>.Success(info));
            }

            // Default fallback: 10 capacity, 50% ratio (ceiling 5), 5 available slots
            return Task.FromResult(Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: 0m)));
        }
    }

    private sealed class TestParkingPricingProvider : IParkingPricingProvider
    {
        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(
            Guid lotId,
            VehicleGroup vehicleGroup,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<ParkingPricingInfo>.Success(
                new ParkingPricingInfo(lotId, vehicleGroup, 20000m, "Block", 60, 0m, "VND")));
        }
    }

    private (ServiceProvider Provider, TestClock Clock, TestParkingLotCapacityProvider CapacityProvider) CreateTestEnvironment()
    {
        var builder = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            MaxPoolSize = 250
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

        services.AddSingleton<IClock>(testClock);
        services.AddSingleton<IVehicleOwnershipClient>(new TestVehicleOwnershipClient());
        services.AddSingleton<IParkingLotCapacityProvider>(capacityProvider);
        services.AddSingleton<IParkingPricingProvider>(new TestParkingPricingProvider());
        services.AddSingleton(new BookingCapacityOptions
        {
            LeadTimeMinutes = 30,
            GracePeriodMinutes = 15,
            TempHoldDurationMinutes = 10,
            LockTimeoutSeconds = 10
        });

        var provider = services.BuildServiceProvider(validateScopes: true);
        return (provider, testClock, capacityProvider);
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
    // I1: Trần 1, 100 yêu cầu song song, khung giống nhau
    // Kỳ vọng: Đúng 1 thành công, 99 CapacityExceeded, đúng 1 dòng DB, lặp lại ổn định
    // =========================================================================
    [Fact]
    public async Task I1_Single_available_slot_with_100_concurrent_requests_allows_exactly_one_success()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, capacityProvider) = CreateTestEnvironment();
            await using (provider)
            {
                await EnsureMigratedAsync(provider);

                const int iterations = 3; // Running 3 full iterations of 100 concurrent requests
                for (var iter = 0; iter < iterations; iter++)
                {
                    var lotId = Guid.NewGuid();
                    // 1 car capacity, 100% ratio -> ceiling = 1
                    capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 1);

                    const int concurrentRequests = 100;
                    var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
                    {
                        await startBarrier.Task;

                        await using var scope = provider.CreateAsyncScope();
                        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

                        var command = new CreateTemporaryHoldCommand(
                            ReservationId: Guid.NewGuid(),
                            UserId: Guid.NewGuid(),
                            VehicleId: Guid.NewGuid(),
                            PlateNumber: $"51F-{iter:D2}-{i:D5}",
                            LotId: lotId,
                            VehicleGroup: VehicleGroup.Car,
                            ReservationType: ReservationType.Scheduled,
                            AppointmentTime: clock.UtcNow.AddHours(3),
                            DurationMinutes: 60,
                            PrepaidAmount: 20000m);

                        return await sender.Send(command);
                    }).ToList();

                    startBarrier.SetResult();
                    var results = await Task.WhenAll(tasks);

                    var successes = results.Where(r => r.IsSuccess).ToList();
                    var failures = results.Where(r => !r.IsSuccess).ToList();

                    Assert.Single(successes);
                    Assert.Equal(99, failures.Count);
                    Assert.All(failures, f => Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, f.Error!.Code));

                    // Verify DB has exactly 1 row for this lot
                    await using var verifyScope = provider.CreateAsyncScope();
                    var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                    var dbCount = await verifyDb.Reservations.CountAsync(r => r.LotId == lotId);
                    Assert.Equal(1, dbCount);
                }
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // I2: Trần 10 đã có 3 đặt chỗ (còn 7): 50 yêu cầu -> đúng 7 thành công; 5 yêu cầu -> cả 5 thành công
    // =========================================================================
    [Fact]
    public async Task I2_Ceiling_10_with_3_existing_reservations_allocates_exactly_remaining_capacity()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // 10 car capacity, 100% ratio -> ceiling = 10
            capacityProvider.SetLotCapacity(lotId, validCapacity: 10, ratioPercent: 100, availableSlots: 10);

            var appointmentTime = clock.UtcNow.AddHours(2);

            // Pre-seed 3 active reservations
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                for (var i = 0; i < 3; i++)
                {
                    var res = Reservation.CreateTemporaryHold(
                        id: Guid.NewGuid(),
                        userId: Guid.NewGuid(),
                        vehicleId: Guid.NewGuid(),
                        plateNumber: $"51F-SEED{i}",
                        lotId: lotId,
                        vehicleGroup: VehicleGroup.Car,
                        reservationType: ReservationType.Scheduled,
                        appointmentTime: appointmentTime,
                        prepaidAmount: 20000m,
                        createdAt: clock.UtcNow,
                        durationMinutes: 60,
                        tempHoldExpiresAt: clock.UtcNow.AddMinutes(10));
                    res.Confirm(clock.UtcNow);
                    seedDb.Reservations.Add(res);
                }
                await seedDb.SaveChangesAsync();
            }

            // Phase A: 50 concurrent requests competing for the 7 remaining slots
            var startBarrierA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasksA = Enumerable.Range(0, 50).Select(async i =>
            {
                await startBarrierA.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
                var cmd = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: $"51A-{i:D4}",
                    LotId: lotId,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: ReservationType.Scheduled,
                    AppointmentTime: appointmentTime,
                    DurationMinutes: 60,
                    PrepaidAmount: 20000m);
                return await sender.Send(cmd);
            }).ToList();

            startBarrierA.SetResult();
            var resultsA = await Task.WhenAll(tasksA);

            Assert.Equal(7, resultsA.Count(r => r.IsSuccess));
            Assert.Equal(43, resultsA.Count(r => !r.IsSuccess));

            // Verify total in DB is exactly 10
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                Assert.Equal(10, await verifyDb.Reservations.CountAsync(r => r.LotId == lotId));
            }

            // Phase B: Lot B with ceiling 10, pre-seed 3 active -> 7 remaining. Send 5 requests -> ALL 5 must succeed!
            var lotIdB = Guid.NewGuid();
            capacityProvider.SetLotCapacity(lotIdB, validCapacity: 10, ratioPercent: 100, availableSlots: 10);

            await using (var seedScopeB = provider.CreateAsyncScope())
            {
                var seedDbB = seedScopeB.ServiceProvider.GetRequiredService<BookingDbContext>();
                for (var i = 0; i < 3; i++)
                {
                    var res = Reservation.CreateTemporaryHold(
                        id: Guid.NewGuid(),
                        userId: Guid.NewGuid(),
                        vehicleId: Guid.NewGuid(),
                        plateNumber: $"51B-SEED{i}",
                        lotId: lotIdB,
                        vehicleGroup: VehicleGroup.Car,
                        reservationType: ReservationType.Scheduled,
                        appointmentTime: appointmentTime,
                        prepaidAmount: 20000m,
                        createdAt: clock.UtcNow,
                        durationMinutes: 60,
                        tempHoldExpiresAt: clock.UtcNow.AddMinutes(10));
                    res.Confirm(clock.UtcNow);
                    seedDbB.Reservations.Add(res);
                }
                await seedDbB.SaveChangesAsync();
            }

            var startBarrierB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasksB = Enumerable.Range(0, 5).Select(async i =>
            {
                await startBarrierB.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
                var cmd = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: $"51B-REQ{i}",
                    LotId: lotIdB,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: ReservationType.Scheduled,
                    AppointmentTime: appointmentTime,
                    DurationMinutes: 60,
                    PrepaidAmount: 20000m);
                return await sender.Send(cmd);
            }).ToList();

            startBarrierB.SetResult();
            var resultsB = await Task.WhenAll(tasksB);

            // All 5 must succeed (no over-rejection when N < R)
            Assert.Equal(5, resultsB.Count(r => r.IsSuccess));
            Assert.Equal(0, resultsB.Count(r => !r.IsSuccess));
        }
    }

    // =========================================================================
    // I3: Hai bãi chạy song song độc lập, không khóa lẫn nhau
    // =========================================================================
    [Fact]
    public async Task I3_Two_distinct_parking_lots_operate_independently_without_cross_locking()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lot1 = Guid.NewGuid();
            var lot2 = Guid.NewGuid();
            capacityProvider.SetLotCapacity(lot1, validCapacity: 2, ratioPercent: 100, availableSlots: 2);
            capacityProvider.SetLotCapacity(lot2, validCapacity: 3, ratioPercent: 100, availableSlots: 3);

            var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks1 = Enumerable.Range(0, 10).Select(async i =>
            {
                await startBarrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
                return await sender.Send(new CreateTemporaryHoldCommand(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"51A-{i}", lot1,
                    VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            });

            var tasks2 = Enumerable.Range(0, 10).Select(async i =>
            {
                await startBarrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
                return await sender.Send(new CreateTemporaryHoldCommand(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"51B-{i}", lot2,
                    VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            });

            startBarrier.SetResult();
            var allResults = await Task.WhenAll(tasks1.Concat(tasks2));

            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var countLot1 = await verifyDb.Reservations.CountAsync(r => r.LotId == lot1);
            var countLot2 = await verifyDb.Reservations.CountAsync(r => r.LotId == lot2);

            Assert.Equal(2, countLot1);
            Assert.Equal(3, countLot2);
        }
    }

    // =========================================================================
    // I4: Khung không giao không chặn nhau (sát biên kết thúc = bắt đầu thì thành công)
    // =========================================================================
    [Fact]
    public async Task I4_Non_overlapping_and_adjacent_intervals_do_not_block_each_other()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 1
            capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 1);

            await using var scope = provider.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

            // Booking 1: appointment 10:30, duration 30m -> lead 30m -> window [10:00, 11:00)
            var res1 = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-00001", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddMinutes(30), 30, 20000m));
            Assert.True(res1.IsSuccess);

            // Booking 2: appointment 11:30, duration 30m -> lead 30m -> window [11:00, 12:00)
            // Sát biên: end của 1 = 11:00 == start của 2 = 11:00 -> KHÔNG GIAO -> THÀNH CÔNG!
            var res2 = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-00002", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddMinutes(90), 30, 20000m));
            Assert.True(res2.IsSuccess);

            // Booking 3: appointment 11:29, duration 30m -> window starts at 10:59 -> overlaps by 1 min with Booking 1 [10:00, 11:00)
            // Chồng 1 phút -> BỊ TỪ CHỐI!
            var res3 = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-00003", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddMinutes(89), 30, 20000m));
            Assert.False(res3.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, res3.Error!.Code);
        }
    }

    // =========================================================================
    // I5: Hỗn hợp giữ chỗ khi đang tới và đặt trước chồng khung chạy song song
    // Invariant: Tổng số đặt chỗ giao nhau không vượt trần
    // =========================================================================
    [Fact]
    public async Task I5_Mixed_instant_and_scheduled_reservations_never_exceed_ceiling()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 4
            capacityProvider.SetLotCapacity(lotId, validCapacity: 4, ratioPercent: 100, availableSlots: 4);

            var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // 15 HoldWhileDriving requests + 15 Scheduled requests (total 30)
            var tasks = Enumerable.Range(0, 30).Select(async i =>
            {
                await startBarrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

                var isScheduled = i % 2 == 0;
                var command = new CreateTemporaryHoldCommand(
                    ReservationId: Guid.NewGuid(),
                    UserId: Guid.NewGuid(),
                    VehicleId: Guid.NewGuid(),
                    PlateNumber: $"51MIX-{i:D3}",
                    LotId: lotId,
                    VehicleGroup: VehicleGroup.Car,
                    ReservationType: isScheduled ? ReservationType.Scheduled : ReservationType.HoldWhileDriving,
                    AppointmentTime: clock.UtcNow.AddMinutes(30),
                    DurationMinutes: isScheduled ? 60 : null,
                    PrepaidAmount: 20000m);

                return await sender.Send(command);
            }).ToList();

            startBarrier.SetResult();
            var results = await Task.WhenAll(tasks);

            var successfulCount = results.Count(r => r.IsSuccess);
            Assert.Equal(4, successfulCount);

            // Invariant assertion on DB
            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var activeHolds = await verifyDb.Reservations.Where(r => r.LotId == lotId).ToListAsync();
            Assert.Equal(4, activeHolds.Count);
        }
    }

    // =========================================================================
    // I6: Trạng thái hủy/hết hạn giải phóng suất
    // =========================================================================
    [Fact]
    public async Task I6_Status_transitions_and_expired_temporary_holds_release_capacity()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 1
            capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 1);

            await using var scope1 = provider.CreateAsyncScope();
            var sender1 = scope1.ServiceProvider.GetRequiredService<MediatR.ISender>();

            var res1 = await sender1.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-RELEASE", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            Assert.True(res1.IsSuccess);

            // A second request now fails because 1 slot is occupied
            await using var scope2 = provider.CreateAsyncScope();
            var sender2 = scope2.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res2 = await sender2.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-BLOCKED", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            Assert.False(res2.IsSuccess);

            // Scenario A: Cancel the reservation
            await using (var cancelScope = provider.CreateAsyncScope())
            {
                var db = cancelScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var entity = await db.Reservations.FindAsync(res1.Value!.Id);
                entity!.Cancel(clock.UtcNow, CancelReason.CustomerCancelled);
                await db.SaveChangesAsync();
            }

            // Next request succeeds because slot was released!
            await using var scope3 = provider.CreateAsyncScope();
            var sender3 = scope3.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res3 = await sender3.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-CANCEL", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            Assert.True(res3.IsSuccess);

            // Scenario B: Let the 10-minute temporary hold expire (Assumption A1: temp_hold_expires_at <= now is not counted)
            clock.UtcNow = clock.UtcNow.AddMinutes(15); // Advance clock past the 10-minute hold expiry

            // Even without running the cleanup job, an expired temporary hold does not block new reservations!
            await using var scope4 = provider.CreateAsyncScope();
            var sender4 = scope4.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res4 = await sender4.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-EXPIRY", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            Assert.True(res4.IsSuccess);
        }
    }

    // =========================================================================
    // I6A: TemporaryHold quá hạn (temp_hold_expires_at < now) dù hold_end_at còn xa cũng không chiếm suất
    // =========================================================================
    [Fact]
    public async Task I6A_Expired_temporary_hold_with_future_hold_end_at_does_not_block_new_holds()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 1, Available slots 5
            capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 5);

            var appointment = clock.UtcNow.AddHours(2);
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var expiredHold = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-EXPHOLD", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, appointment, 20000m, clock.UtcNow.AddMinutes(-20),
                    durationMinutes: 60, tempHoldExpiresAt: clock.UtcNow.AddMinutes(-1),
                    windowStartAt: appointment.AddMinutes(-30), windowEndAt: appointment.AddMinutes(60), holdEndAt: appointment.AddMinutes(15));
                db.Reservations.Add(expiredHold);
                await db.SaveChangesAsync();
            }

            await using var scope = provider.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-NEWWIN", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, appointment, 60, 20000m));

            Assert.True(res.IsSuccess);
        }
    }

    // =========================================================================
    // I6B: Reservation Confirmed có temp_hold_expires_at trong quá khứ vẫn được tính còn hiệu lực
    // =========================================================================
    [Fact]
    public async Task I6B_Confirmed_reservation_with_past_temp_hold_expires_at_is_still_counted_active()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 1, Available slots 5
            capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 5);

            var appointment = clock.UtcNow.AddHours(2);
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var confirmedRes = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-CONF", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, appointment, 20000m, clock.UtcNow.AddMinutes(-30),
                    durationMinutes: 60, tempHoldExpiresAt: clock.UtcNow.AddMinutes(-10),
                    windowStartAt: appointment.AddMinutes(-30), windowEndAt: appointment.AddMinutes(60), holdEndAt: appointment.AddMinutes(15));
                confirmedRes.Confirm(clock.UtcNow.AddMinutes(-20));
                db.Reservations.Add(confirmedRes);
                await db.SaveChangesAsync();
            }

            await using var scope = provider.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-TRY", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, appointment, 60, 20000m));

            Assert.False(res.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, res.Error!.Code);
        }
    }

    // =========================================================================
    // I7: Đơn giản hóa BR-02.e: A [0,4), B [4,8), trần 2, C [2,6) giao cả A và B -> BỊ TỪ CHỐI
    // =========================================================================
    [Fact]
    public async Task I7_BR02e_simplification_counts_all_overlapping_reservations_even_if_they_do_not_overlap_each_other()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling = 2
            capacityProvider.SetLotCapacity(lotId, validCapacity: 2, ratioPercent: 100, availableSlots: 2);

            var t0 = clock.UtcNow.AddDays(1);

            // Seed A: [00:00, 04:00) -> appointment at t0 + 30m, duration 210m -> window [t0, t0 + 4h)
            // Seed B: [04:00, 08:00) -> appointment at t0 + 4h30m, duration 210m -> window [t0 + 4h, t0 + 8h)
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var resA = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-RESA", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, t0.AddMinutes(30), 20000m, clock.UtcNow,
                    durationMinutes: 210, tempHoldExpiresAt: clock.UtcNow.AddMinutes(10),
                    windowStartAt: t0, windowEndAt: t0.AddHours(4), holdEndAt: t0.AddMinutes(45));
                resA.Confirm(clock.UtcNow);

                var resB = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51B-RESB", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, t0.AddHours(4).AddMinutes(30), 20000m, clock.UtcNow,
                    durationMinutes: 210, tempHoldExpiresAt: clock.UtcNow.AddMinutes(10),
                    windowStartAt: t0.AddHours(4), windowEndAt: t0.AddHours(8), holdEndAt: t0.AddHours(4).AddMinutes(45));
                resB.Confirm(clock.UtcNow);

                db.Reservations.AddRange(resA, resB);
                await db.SaveChangesAsync();
            }

            // New reservation C has window [02:00, 06:00)
            // Overlaps with A [0,4) because max(0,2) < min(4,6) => 2 < 4
            // Overlaps with B [4,8) because max(4,2) < min(8,6) => 4 < 6
            // Total overlapping reservations counted = 2 (A and B).
            // Since ceiling = 2, C must be REJECTED!
            await using var scope = provider.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var resC = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51C-RESC", lotId,
                VehicleGroup.Car, ReservationType.Scheduled, t0.AddHours(2).AddMinutes(30), 210, 20000m));

            Assert.False(resC.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.CapacityExceeded.Code, resC.Error!.Code);
        }
    }

    // =========================================================================
    // I8: Điều kiện (ii) với dữ liệu thật (AvailableSlotCount)
    // =========================================================================
    [Fact]
    public async Task I8_Condition_ii_evaluates_active_holds_against_available_slot_count_when_window_has_started()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 10, but AvailableSlotCount right now is only 1!
            capacityProvider.SetLotCapacity(lotId, validCapacity: 10, ratioPercent: 100, availableSlots: 1);

            // Request 1: Instant hold (window has started at now).
            // Active holds before = 0 < 1 -> Succeeds!
            await using var scope1 = provider.CreateAsyncScope();
            var sender1 = scope1.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res1 = await sender1.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-AVAIL1", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(15), null, 20000m));
            Assert.True(res1.IsSuccess);

            // Request 2: Also instant hold at now.
            // Active holds now = 1 == availableSlots (1) -> Fails with NoAvailableSlots!
            await using var scope2 = provider.CreateAsyncScope();
            var sender2 = scope2.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res2 = await sender2.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-AVAIL2", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(15), null, 20000m));
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.NoAvailableSlots.Code, res2.Error!.Code);
        }
    }

    // =========================================================================
    // I8A: Điều kiện (ii) - Reservation tương lai không chiếm suất của thời điểm 'now'
    // =========================================================================
    [Fact]
    public async Task I8A_Condition_ii_future_reservation_does_not_consume_current_available_slots()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 10, AvailableSlotCount = 1
            capacityProvider.SetLotCapacity(lotId, validCapacity: 10, ratioPercent: 100, availableSlots: 1);

            // Seed 1 Confirmed reservation 3 days in future
            var futureTime = clock.UtcNow.AddDays(3);
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var futureRes = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-FUTURE", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, futureTime, 20000m, clock.UtcNow,
                    durationMinutes: 120, tempHoldExpiresAt: clock.UtcNow.AddMinutes(10),
                    windowStartAt: futureTime.AddMinutes(-30), windowEndAt: futureTime.AddMinutes(120), holdEndAt: futureTime.AddMinutes(15));
                futureRes.Confirm(clock.UtcNow);
                db.Reservations.Add(futureRes);
                await db.SaveChangesAsync();
            }

            // New instant reservation at now:
            // Condition (i): No overlap with future window in 3 days.
            // Condition (ii): Active holds at now = 0 < availableSlots 1 -> SUCCEEDS!
            await using var scope = provider.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-NOW", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(15), null, 20000m));

            Assert.True(res.IsSuccess);

            // Verify in DB that exactly 2 reservations exist (1 future, 1 new hold)
            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var count = await verifyDb.Reservations.CountAsync(r => r.LotId == lotId);
            Assert.Equal(2, count);
        }
    }

    // =========================================================================
    // I8B: Điều kiện (ii) - Kiểm tra biên window_start_at == now và window_end_at == now
    // =========================================================================
    [Fact]
    public async Task I8B_Condition_ii_boundary_checks_and_active_at_now_counted()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            // Ceiling 10, Available slots = 1
            capacityProvider.SetLotCapacity(lotId, validCapacity: 10, ratioPercent: 100, availableSlots: 1);

            // Seed ended reservation where window_end_at == now
            // Half-open [start, end) means at 'now' it has ALREADY ENDED -> not active at now!
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var endedRes = Reservation.CreateTemporaryHold(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-ENDED", lotId,
                    VehicleGroup.Car, ReservationType.Scheduled, clock.UtcNow.AddMinutes(-60), 20000m, clock.UtcNow,
                    durationMinutes: 60, tempHoldExpiresAt: clock.UtcNow.AddMinutes(-50),
                    windowStartAt: clock.UtcNow.AddMinutes(-90), windowEndAt: clock.UtcNow, holdEndAt: clock.UtcNow.AddMinutes(-45));
                endedRes.Confirm(clock.UtcNow.AddMinutes(-50));
                db.Reservations.Add(endedRes);
                await db.SaveChangesAsync();
            }

            // Since endedRes window_end_at == now, it does not occupy available slot at now.
            // A new instant hold succeeds!
            await using var scope1 = provider.CreateAsyncScope();
            var sender1 = scope1.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res1 = await sender1.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-ACTIVE", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(15), null, 20000m));
            Assert.True(res1.IsSuccess);

            // Now there is 1 active hold at 'now' (res1).
            // Another instant request must be REJECTED because active holds (1) >= availableSlots (1)!
            await using var scope2 = provider.CreateAsyncScope();
            var sender2 = scope2.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res2 = await sender2.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-REJECT", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(15), null, 20000m));
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.NoAvailableSlots.Code, res2.Error!.Code);
        }
    }

    // =========================================================================
    // I9: Idempotent - 20 yêu cầu song song cùng ReservationId
    // Kỳ vọng: Đúng 1 dòng DB, chỉ tiêu 1 suất, mọi lời gọi nhận cùng hold
    // =========================================================================
    [Fact]
    public async Task I9_Concurrent_idempotent_requests_with_same_id_consume_single_slot()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            capacityProvider.SetLotCapacity(lotId, validCapacity: 5, ratioPercent: 100, availableSlots: 5);

            var sharedReservationId = Guid.NewGuid();
            var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, 20).Select(async i =>
            {
                await startBarrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
                return await sender.Send(new CreateTemporaryHoldCommand(
                    sharedReservationId, Guid.NewGuid(), Guid.NewGuid(), "51IDEM-01", lotId,
                    VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));
            }).ToList();

            startBarrier.SetResult();
            var results = await Task.WhenAll(tasks);

            // Every single caller receives success!
            Assert.All(results, r => Assert.True(r.IsSuccess));
            Assert.All(results, r => Assert.Equal(sharedReservationId, r.Value!.Id));

            // Verify DB has strictly 1 record
            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var count = await verifyDb.Reservations.CountAsync(r => r.LotId == lotId);
            Assert.Equal(1, count);
        }
    }

    // =========================================================================
    // I10: Rollback và nhả khóa sau lỗi giữa chừng
    // =========================================================================
    [Fact]
    public async Task I10_Transaction_rollback_releases_lock_allowing_subsequent_request_to_succeed()
    {
        var (provider, clock, capacityProvider) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            capacityProvider.SetLotCapacity(lotId, validCapacity: 1, ratioPercent: 100, availableSlots: 1);

            // Simulate mid-transaction error after acquiring lock
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                // Acquire lock
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO booking.reservation_capacity (lot_id, vehicle_group, updated_at) VALUES ({0}, {1}, {2}) ON CONFLICT DO NOTHING;",
                    lotId, VehicleGroup.Car, clock.UtcNow);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM booking.reservation_capacity WHERE lot_id = {0} AND vehicle_group = {1} FOR UPDATE;",
                    lotId, VehicleGroup.Car);

                // Intentionally rollback without committing
                await tx.RollbackAsync();
            }

            // Subsequent valid request must acquire lock and succeed cleanly
            await using var normalScope = provider.CreateAsyncScope();
            var sender = normalScope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var res = await sender.Send(new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-NORMAL", lotId,
                VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), null, 20000m));

            Assert.True(res.IsSuccess);
        }
    }

    // =========================================================================
    // I11: Kiểm tra cấu trúc migration reservation_capacity & indexes trong PostgreSQL
    // =========================================================================
    [Fact]
    public async Task I11_Migration_creates_reservation_capacity_table_with_composite_pk_and_expected_indexes()
    {
        var (provider, _, _) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // Query PostgreSQL information_schema for primary key of reservation_capacity
            var pkColumns = new List<string>();
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = """
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.table_schema = 'booking' AND tc.table_name = 'reservation_capacity' AND tc.constraint_type = 'PRIMARY KEY'
                ORDER BY kcu.ordinal_position;
                """;

            await db.Database.OpenConnectionAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pkColumns.Add(reader.GetString(0));
            }

            Assert.Equal(["lot_id", "vehicle_group"], pkColumns);
        }
    }

    // =========================================================================
    // I12: Kiểm chứng độ nhạy của test (Naive benchmark không khóa)
    // Chứng minh rằng nếu không khóa hàng, race condition sẽ gây nhận quá suất!
    // =========================================================================
    [Fact]
    public async Task I12_Naive_non_locking_implementation_fails_and_overbooks_proving_test_sensitivity()
    {
        var (provider, clock, _) = CreateTestEnvironment();
        await using (provider)
        {
            await EnsureMigratedAsync(provider);

            var lotId = Guid.NewGuid();
            const int ceiling = 1;
            const int concurrentRequests = 40;

            // Run a naive non-locking check-then-insert with an artificial delay to widen the race window
            var startBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
            {
                await startBarrier.Task;

                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                // NAIVE STEP 1: Count active overlapping holds WITHOUT acquiring a lock
                var count = await db.Reservations.CountAsync(r => r.LotId == lotId);

                // Artificial delay to widen the race window between check and insert
                await Task.Delay(100);

                if (count < ceiling)
                {
                    var reservation = Reservation.CreateTemporaryHold(
                        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"51RACE-{i:D2}", lotId,
                        VehicleGroup.Car, ReservationType.HoldWhileDriving, clock.UtcNow.AddMinutes(20), 20000m, clock.UtcNow);
                    db.Reservations.Add(reservation);
                    await db.SaveChangesAsync();
                    return true;
                }

                return false;
            }).ToList();

            startBarrier.SetResult();
            var results = await Task.WhenAll(tasks);

            var naiveSuccessCount = results.Count(r => r);

            // The naive implementation MUST overbook (> 1 success for 1 slot), proving that the race condition exists
            // and that our test suite reliably detects concurrency bugs!
            Assert.True(naiveSuccessCount > 1,
                $"Expected naive non-locking implementation to overbook (> 1 success), but got {naiveSuccessCount}. " +
                "This proves the test harness sensitivity.");
        }
    }
}
