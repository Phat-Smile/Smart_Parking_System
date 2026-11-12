using System.Collections.Concurrent;
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
public sealed class ReservationLimitsAndPlateOverlapIntegrationTests(PostgreSqlContainerFixture postgres)
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

            // High default capacity (100 capacity, 100% ratio, 100 available) so lot capacity never blocks limit/overlap tests
            return Task.FromResult(Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(100, 100, 100, ReservationEnabled: true, DepositAmount: 0m)));
        }
    }

    private (ServiceProvider Provider, TestClock Clock, TestParkingLotCapacityProvider CapacityProvider) CreateTestEnvironment(
        int maxActiveReservationsPerUser = 3)
    {
        var builder = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            MaxPoolSize = 120,
            ConnectionIdleLifetime = 5,
            ConnectionPruningInterval = 2
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
            LockTimeoutSeconds = 10,
            MaxActiveReservationsPerUser = maxActiveReservationsPerUser
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

    private static CreateTemporaryHoldCommand CreateCommand(
        Guid? reservationId = null,
        Guid? userId = null,
        Guid? vehicleId = null,
        string plateNumber = "51F-111.11",
        Guid? lotId = null,
        DateTimeOffset? appointmentTime = null,
        int? durationMinutes = 60,
        decimal prepaidAmount = 20000m)
    {
        return new CreateTemporaryHoldCommand(
            ReservationId: reservationId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            VehicleId: vehicleId ?? Guid.NewGuid(),
            PlateNumber: plateNumber,
            LotId: lotId ?? Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.Scheduled,
            AppointmentTime: appointmentTime ?? BaseTime.AddHours(2),
            DurationMinutes: durationMinutes,
            PrepaidAmount: prepaidAmount);
    }

    // =========================================================================
    // BR-1: Active Reservation Limits per User (RL-01 to RL-10)
    // =========================================================================

    [Fact]
    public async Task RL_01_RL_02_User_Can_Create_Up_To_Max_Active_Reservations()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var userId = Guid.NewGuid();

            // 1st reservation: allowed
            var cmd1 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(1), plateNumber: "51F-001.01");
            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2nd reservation: allowed
            var cmd2 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(3), plateNumber: "51F-001.02");
            var res2 = await mediator.Send(cmd2);
            Assert.True(res2.IsSuccess);

            // 3rd reservation: allowed (reaches limit of 3)
            var cmd3 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(5), plateNumber: "51F-001.03");
            var res3 = await mediator.Send(cmd3);
            Assert.True(res3.IsSuccess);

            // 4th reservation: REJECTED with UserActiveReservationsLimitExceeded
            var cmd4 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(7), plateNumber: "51F-001.04");
            var res4 = await mediator.Send(cmd4);
            Assert.False(res4.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.UserActiveReservationsLimitExceeded.Code, res4.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task RL_05_RL_06_Non_Active_Reservations_Do_Not_Count_Toward_Limit()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var userId = Guid.NewGuid();

            // Create 3 reservations
            var cmd1 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(1), plateNumber: "51F-002.01");
            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            var cmd2 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(3), plateNumber: "51F-002.02");
            var res2 = await mediator.Send(cmd2);
            Assert.True(res2.IsSuccess);

            var cmd3 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(5), plateNumber: "51F-002.03");
            var res3 = await mediator.Send(cmd3);
            Assert.True(res3.IsSuccess);

            // Cancel res1 in DB
            var entity1 = await dbContext.Reservations.FirstAsync(r => r.Id == cmd1.ReservationId);
            entity1.Cancel(clock.UtcNow.AddMinutes(1), CancelReason.CustomerCancelled);

            // Mark res2 as Confirmed then Used in DB
            var entity2 = await dbContext.Reservations.FirstAsync(r => r.Id == cmd2.ReservationId);
            entity2.Confirm(clock.UtcNow.AddMinutes(1));
            entity2.Use(clock.UtcNow.AddMinutes(2), Guid.NewGuid());

            await dbContext.SaveChangesAsync();

            // Now userId has only 1 active reservation (res3). Can create 2 more!
            var cmd4 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(7), plateNumber: "51F-002.04");
            var res4 = await mediator.Send(cmd4);
            Assert.True(res4.IsSuccess);

            var cmd5 = CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(9), plateNumber: "51F-002.05");
            var res5 = await mediator.Send(cmd5);
            Assert.True(res5.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task RL_07_Different_Users_Have_Independent_Limits()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            // User A creates 3 reservations (reaches limit)
            for (var i = 1; i <= 3; i++)
            {
                var cmdA = CreateCommand(userId: userA, appointmentTime: BaseTime.AddHours(i * 2), plateNumber: $"51F-USA.0{i}");
                var resA = await mediator.Send(cmdA);
                Assert.True(resA.IsSuccess);
            }

            // User A is blocked
            var cmdABlocked = CreateCommand(userId: userA, appointmentTime: BaseTime.AddHours(8), plateNumber: "51F-USA.04");
            var resABlocked = await mediator.Send(cmdABlocked);
            Assert.False(resABlocked.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.UserActiveReservationsLimitExceeded.Code, resABlocked.Error.Code);

            // User B is NOT blocked
            var cmdB1 = CreateCommand(userId: userB, appointmentTime: BaseTime.AddHours(2), plateNumber: "51F-USB.01");
            var resB1 = await mediator.Send(cmdB1);
            Assert.True(resB1.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task RL_08_User_Limit_Applies_Across_Different_Parking_Lots()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var userId = Guid.NewGuid();
            var lot1 = Guid.NewGuid();
            var lot2 = Guid.NewGuid();
            var lot3 = Guid.NewGuid();
            var lot4 = Guid.NewGuid();

            // 1 at lot 1, 1 at lot 2, 1 at lot 3 -> total 3 across lots
            var res1 = await mediator.Send(CreateCommand(userId: userId, lotId: lot1, plateNumber: "51F-LOT.01", appointmentTime: BaseTime.AddHours(1)));
            Assert.True(res1.IsSuccess);

            var res2 = await mediator.Send(CreateCommand(userId: userId, lotId: lot2, plateNumber: "51F-LOT.02", appointmentTime: BaseTime.AddHours(3)));
            Assert.True(res2.IsSuccess);

            var res3 = await mediator.Send(CreateCommand(userId: userId, lotId: lot3, plateNumber: "51F-LOT.03", appointmentTime: BaseTime.AddHours(5)));
            Assert.True(res3.IsSuccess);

            // 4th at lot 4 is rejected
            var res4 = await mediator.Send(CreateCommand(userId: userId, lotId: lot4, plateNumber: "51F-LOT.04", appointmentTime: BaseTime.AddHours(7)));
            Assert.False(res4.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.UserActiveReservationsLimitExceeded.Code, res4.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // BR-2: Plate Overlap Prevention (PO-01 to PO-12)
    // =========================================================================

    [Fact]
    public async Task PO_01_PO_06_Non_Overlapping_And_Adjacent_Intervals_For_Same_Plate_Are_Allowed()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var plate = "51F-ADJACENT";
            // Res 1: Appointment at 12:00, duration 60m. Window: [11:30, 13:00)
            var res1 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2), // 12:00
                durationMinutes: 60));
            Assert.True(res1.IsSuccess);

            // Res 2: Appointment at 14:00, duration 60m. Window: [13:30, 15:00) -> non-overlapping with [11:30, 13:00)
            var res2 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(4), // 14:00
                durationMinutes: 60));
            Assert.True(res2.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_02_PO_03_Overlapping_Windows_For_Same_Plate_Are_Rejected()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var plate = "51F-OVERLAP";
            // Res 1: Appointment at 12:00, duration 60m. Window: [11:30, 13:00)
            var res1 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60));
            Assert.True(res1.IsSuccess);

            // Res 2: Overlapping appointment at 12:30, duration 60m. Window: [12:00, 13:30)
            var res2 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2).AddMinutes(30),
                durationMinutes: 60));

            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_11_Plate_Overlap_Handles_Case_And_Whitespace_Normalization()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // Res 1 with standard uppercase plate
            var res1 = await mediator.Send(CreateCommand(
                plateNumber: "29A-888.88",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60));
            Assert.True(res1.IsSuccess);

            // Res 2 with lowercase and surrounding whitespace
            var res2 = await mediator.Send(CreateCommand(
                plateNumber: "  29a-888.88  ",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60));

            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_12_Same_Plate_Booked_By_Different_User_In_Overlapping_Window_Is_Rejected()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            var plate = "30E-777.77";

            // User 1 books the plate
            var res1 = await mediator.Send(CreateCommand(
                userId: user1,
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60));
            Assert.True(res1.IsSuccess);

            // User 2 tries to book the same plate at overlapping time
            var res2 = await mediator.Send(CreateCommand(
                userId: user2,
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60));

            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_04_Existing_Window_Contained_Inside_New_Window_Is_Rejected()
    {
        // Case 4: Incoming reservation window completely contains existing reservation window
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var plate = "51F-CONTAIN";
            // Res 1 (contained): Appointment at 12:00, duration 30m -> [11:30, 12:30)
            var res1 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 30));
            Assert.True(res1.IsSuccess);

            // Res 2 (containing): Appointment at 11:30, duration 120m -> [11:00, 13:30)
            var res2 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(1).AddMinutes(30),
                durationMinutes: 120));

            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_08_Different_Plates_With_Overlapping_Windows_Are_Allowed()
    {
        // Case 8: Different plates targeting the exact same time window are not rejected by plate overlap
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var appointment = BaseTime.AddHours(3);

            var res1 = await mediator.Send(CreateCommand(
                plateNumber: "51F-PLATE.01",
                appointmentTime: appointment,
                durationMinutes: 60));
            Assert.True(res1.IsSuccess);

            var res2 = await mediator.Send(CreateCommand(
                plateNumber: "51F-PLATE.02",
                appointmentTime: appointment,
                durationMinutes: 60));
            Assert.True(res2.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_09_PO_10_Cancelled_Or_Expired_Reservations_Do_Not_Block_New_Reservation_For_Same_Plate()
    {
        // Cases 9 & 10: Ineligible existing reservations (Cancelled, Expired) do not trigger plate overlap
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, clock, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var plate = "51F-CANCELLED.PLATE";
            var appointment = BaseTime.AddHours(2);

            // Create initial reservation
            var cmd1 = CreateCommand(plateNumber: plate, appointmentTime: appointment, durationMinutes: 60);
            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // Cancel res1
            var entity1 = await dbContext.Reservations.FirstAsync(r => r.Id == cmd1.ReservationId);
            entity1.Cancel(clock.UtcNow.AddMinutes(1), CancelReason.CustomerCancelled);
            await dbContext.SaveChangesAsync();

            // Now create a new reservation for the same plate in the exact same time window: MUST SUCCEED
            var cmd2 = CreateCommand(plateNumber: plate, appointmentTime: appointment, durationMinutes: 60);
            var res2 = await mediator.Send(cmd2);
            Assert.True(res2.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PO_11_HoldWindow_Does_Not_Govern_Plate_Overlap_Conflict()
    {
        // Case 11: Verifies that ReservationWindow governs plate conflict, not HoldWindow
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var plate = "51F-HOLD.CHECK";
            // Res 1: Appointment at 12:00, duration 120m.
            // ReservationWindow: [11:30, 14:00). HoldWindow: [11:30, 12:15).
            var res1 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(2), // 12:00
                durationMinutes: 120));
            Assert.True(res1.IsSuccess);

            // Res 2: Appointment at 13:30, duration 60m.
            // ReservationWindow: [13:00, 14:30). HoldWindow: [13:00, 13:45).
            // Notice: Res 1's HoldWindow ended at 12:15, and Res 2's HoldWindow starts at 13:00.
            // THEIR HOLDWINDOWS DO NOT OVERLAP!
            // BUT their ReservationWindows DO OVERLAP: [11:30, 14:00) vs [13:00, 14:30).
            // Under confirmed business rules, this MUST be rejected as an overlap!
            var res2 = await mediator.Send(CreateCommand(
                plateNumber: plate,
                appointmentTime: BaseTime.AddHours(3).AddMinutes(30), // 13:30
                durationMinutes: 60));

            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // Concurrency Stress Tests (Scenario A, B, C, D)
    // =========================================================================

    [Fact]
    public async Task Concurrency_Scenario_A_30_Concurrent_Requests_For_User_At_Limit_Minus_One_Allows_Exactly_One_Success()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment(maxActiveReservationsPerUser: 3);
            await EnsureMigratedAsync(provider);

            var userId = Guid.NewGuid();

            // Setup: User already has 2 active reservations (limit is 3, 1 slot remains)
            await using (var scope = provider.CreateAsyncScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                var r1 = await mediator.Send(CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(1), plateNumber: "51F-PRE.01"));
                var r2 = await mediator.Send(CreateCommand(userId: userId, appointmentTime: BaseTime.AddHours(3), plateNumber: "51F-PRE.02"));
                Assert.True(r1.IsSuccess);
                Assert.True(r2.IsSuccess);
            }

            // Act: 30 concurrent requests attempt to create the 3rd reservation for this user
            const int totalRequests = 30;
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, totalRequests).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = CreateCommand(
                    userId: userId,
                    plateNumber: $"51F-RACE.A{i:D2}",
                    appointmentTime: BaseTime.AddHours(5 + i * 2),
                    durationMinutes: 60);

                return await mediator.Send(cmd);
            });

            var taskList = tasks.ToList();
            barrier.SetResult();
            var results = await Task.WhenAll(taskList);

            // Assert
            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess && r.Error.Code == ReservationCapacityErrors.UserActiveReservationsLimitExceeded.Code);

            Assert.Equal(1, successCount);
            Assert.Equal(totalRequests - 1, failureCount);

            // Verify in DB: exactly 3 active reservations for this user
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var finalCount = await dbContext.Reservations.CountAsync(r => r.UserId == userId);
                Assert.Equal(3, finalCount);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task Concurrency_Scenario_B_30_Concurrent_Requests_For_Same_Plate_Allows_Exactly_One_Success()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var plate = "43A-RACE.PLATE";
            const int totalRequests = 30;
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // 30 concurrent requests all targeting the EXACT same plate and overlapping time window
            var tasks = Enumerable.Range(0, totalRequests).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = CreateCommand(
                    userId: Guid.NewGuid(), // different users competing for the same plate
                    plateNumber: plate,
                    appointmentTime: BaseTime.AddHours(2),
                    durationMinutes: 60);

                return await mediator.Send(cmd);
            });

            var taskList = tasks.ToList();
            barrier.SetResult();
            var results = await Task.WhenAll(taskList);

            // Assert: Exactly 1 succeeds, 29 fail with PlateReservationOverlap
            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess && r.Error.Code == ReservationCapacityErrors.PlateReservationOverlap.Code);

            Assert.Equal(1, successCount);
            Assert.Equal(totalRequests - 1, failureCount);

            // Verify in DB
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var plateCount = await dbContext.Reservations.CountAsync(r => r.PlateNumber == LicensePlateNormalizer.Normalize(plate));
                Assert.Equal(1, plateCount);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task Concurrency_Scenario_C_Concurrent_Non_Overlapping_Windows_For_Same_Plate_All_Succeed()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            var plate = "60B-NONOVERLAP";
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // 3 concurrent requests for the same plate but with completely separated windows
            var tasks = Enumerable.Range(0, 3).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = CreateCommand(
                    userId: Guid.NewGuid(),
                    plateNumber: plate,
                    appointmentTime: BaseTime.AddHours(2 + i * 4), // 2h, 6h, 10h (no overlap)
                    durationMinutes: 60);

                return await mediator.Send(cmd);
            });

            var taskList = tasks.ToList();
            barrier.SetResult();
            var results = await Task.WhenAll(taskList);

            // All 3 succeed
            Assert.All(results, r => Assert.True(r.IsSuccess));

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var count = await dbContext.Reservations.CountAsync(r => r.PlateNumber == LicensePlateNormalizer.Normalize(plate));
                Assert.Equal(3, count);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task Concurrency_Scenario_D_Isolation_Between_Different_Users_And_Plates()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            const int totalIndependentRequests = 10;
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // 10 concurrent requests with different users and different plates
            var tasks = Enumerable.Range(0, totalIndependentRequests).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = CreateCommand(
                    userId: Guid.NewGuid(),
                    plateNumber: $"72A-ISO.{i:D3}",
                    appointmentTime: BaseTime.AddHours(2),
                    durationMinutes: 60);

                return await mediator.Send(cmd);
            });

            var taskList = tasks.ToList();
            barrier.SetResult();
            var results = await Task.WhenAll(taskList);

            Assert.All(results, r => Assert.True(r.IsSuccess));
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // Canonical License Plate Normalization Integration Tests (PR #152)
    // Testing cross-format plate representation overlap, persistence & concurrency
    // =========================================================================

    [Fact]
    public async Task PlateNorm_01_Existing_Formatted_Plate_Blocks_Incoming_Unformatted_Plate_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with formatted plate "51A-123.45"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51A-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with unformatted plate "51A12345" overlapping window
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51A12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state: exactly 1 reservation for this canonical plate
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51A12345")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_01B_Existing_Plate_With_Internal_Tab_Or_NBSP_Blocks_Incoming_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with internal tab in plate "51G\t123.45"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51G\t123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with canonical plate "51G12345" overlapping window
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51G12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state: exactly 1 reservation for this canonical plate
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51G12345")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_01C_Existing_Plate_With_Actual_NBSP_Blocks_Incoming_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with real Non-Breaking Space (U+00A0, char 160)
            const string rawNbspPlate = "51J\u00A0123.45";
            Assert.Contains('\u00A0', rawNbspPlate);

            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: rawNbspPlate,
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with canonical plate "51J12345" overlapping window
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51J12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state: exactly 1 reservation for this canonical plate
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51J12345")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_01D_Existing_Plate_With_Space_And_Hyphen_Blocks_Incoming_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with space and hyphen format: "51-K 123.45"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51-K 123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with unformatted canonical plate "51K12345"
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51K12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51K12345")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_01E_Preserves_Valid_Letters_And_Digits_Without_Corrupting_Comparison()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with letters and digits including 'S', 'U', 'A': "29S1-123.45"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "29S1-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation missing letter 'S': "291-123.45"
            // If the regex mistakenly stripped 'S', this would collide. It must NOT collide!
            var cmdDifferent = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "291-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var resDifferent = await mediator.Send(cmdDifferent);
            Assert.True(resDifferent.IsSuccess, "Plate '291-123.45' must not collide with '29S1-123.45'; 'S' must be preserved.");

            // 3. Incoming reservation with the actual canonical plate "29S112345" -> MUST COLLIDE and be rejected!
            var cmdSame = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "29S112345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var resSame = await mediator.Send(cmdSame);
            Assert.False(resSame.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, resSame.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_02_Existing_Unformatted_Plate_Blocks_Incoming_Formatted_Plate_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with unformatted plate "51B98765"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51B98765",
                appointmentTime: BaseTime.AddHours(3),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with formatted plate "51B-987.65" overlapping window
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51B-987.65",
                appointmentTime: BaseTime.AddHours(3),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state: exactly 1 reservation for this canonical plate
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51B98765")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_03_Casing_And_Whitespace_Delimiters_Are_Normalized_And_Blocked_When_Overlapping()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with lowercase and surrounding whitespace "  51c-456.78  "
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "  51c-456.78  ",
                appointmentTime: BaseTime.AddHours(4),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with dot notation and uppercase "51C.456.78"
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51C.456.78",
                appointmentTime: BaseTime.AddHours(4),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);

            // Verify DB state: exactly 1 reservation for this canonical plate
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matching = reservations.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51C45678")).ToList();
                Assert.Single(matching);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_04_Equivalent_Plates_With_Non_Overlapping_Windows_Are_Allowed()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with "51D-111.22" for appointment at 2h (window: [1h30m, 3h))
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51D-111.22",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with "51D11122" for appointment at 6h (window: [5h30m, 7h) -> no overlap)
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51D11122",
                appointmentTime: BaseTime.AddHours(6),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.True(res2.IsSuccess);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_05_Cancelled_Reservation_With_Equivalent_Plate_Does_Not_Block_New_Reservation()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            Guid cancelledReservationId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd1 = CreateCommand(
                    userId: Guid.NewGuid(),
                    plateNumber: "51E-333.44",
                    appointmentTime: BaseTime.AddHours(2),
                    durationMinutes: 60);

                var res1 = await mediator.Send(cmd1);
                Assert.True(res1.IsSuccess);
                cancelledReservationId = res1.Value.Id;
            }

            // Cancel the existing reservation
            await using (var cancelScope = provider.CreateAsyncScope())
            {
                var dbContext = cancelScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservation = await dbContext.Reservations.FindAsync(cancelledReservationId);
                Assert.NotNull(reservation);
                reservation.Cancel(BaseTime.AddMinutes(5), CancelReason.CustomerCancelled);
                await dbContext.SaveChangesAsync();
            }

            // Incoming reservation with equivalent unformatted plate "51E33344" overlapping window
            await using (var newScope = provider.CreateAsyncScope())
            {
                var mediator = newScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd2 = CreateCommand(
                    userId: Guid.NewGuid(),
                    plateNumber: "51E33344",
                    appointmentTime: BaseTime.AddHours(2),
                    durationMinutes: 60);

                var res2 = await mediator.Send(cmd2);
                Assert.True(res2.IsSuccess);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task Concurrency_Scenario_E_Mixed_Punctuation_Plate_Collision()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            // 10 concurrent requests for the exact same vehicle with MIXED plate formats:
            var plateVariants = new[]
            {
                "51F-888.88",
                "51F88888",
                "  51f-888.88  ",
                "51F.888.88",
                "51f 888 88",
                "51F-88888",
                "51f88888",
                " 51F.88888 ",
                "51F-888.88",
                "51F88888"
            };

            const int totalRequests = 10;
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, totalRequests).Select(async i =>
            {
                await barrier.Task;
                await using var scope = provider.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

                var cmd = CreateCommand(
                    userId: Guid.NewGuid(),
                    plateNumber: plateVariants[i],
                    appointmentTime: BaseTime.AddHours(2),
                    durationMinutes: 60);

                return await mediator.Send(cmd);
            });

            var taskList = tasks.ToList();
            barrier.SetResult();
            var results = await Task.WhenAll(taskList);

            var successes = results.Where(r => r.IsSuccess).ToList();
            var failures = results.Where(r => !r.IsSuccess).ToList();

            // Advisory lock on canonical normalized plate must guarantee EXACTLY ONE succeeds
            Assert.Single(successes);
            Assert.Equal(9, failures.Count);

            foreach (var failure in failures)
            {
                Assert.True(
                    failure.Error.Code == ReservationCapacityErrors.PlateReservationOverlap.Code ||
                    failure.Error.Code == ReservationCapacityErrors.LockTimeout.Code,
                    $"Unexpected failure code: {failure.Error.Code}");
            }

            // Verify database state: exactly one active reservation exists for this canonical vehicle
            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var allHolds = await dbContext.Reservations.AsNoTracking().ToListAsync();
                var matchingHolds = allHolds.Where(r => LicensePlateNormalizer.AreEquivalent(r.PlateNumber, "51F88888")).ToList();
                Assert.Single(matchingHolds);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    [Fact]
    public async Task PlateNorm_06_Account_Limit_Coexists_With_Plate_Normalization()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment(maxActiveReservationsPerUser: 3);
            await EnsureMigratedAsync(provider);

            var userId = Guid.NewGuid();

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. User creates 1st reservation with "51P-123.45" at 2h
            var cmd1 = CreateCommand(
                userId: userId,
                plateNumber: "51P-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);
            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. User creates 2nd reservation with "51Q-123.45" at 2h
            var cmd2 = CreateCommand(
                userId: userId,
                plateNumber: "51Q-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);
            var res2 = await mediator.Send(cmd2);
            Assert.True(res2.IsSuccess);

            // 3. User attempts 3rd reservation with unformatted "51P12345" overlapping 1st -> REJECTED by PlateReservationOverlap
            var cmdOverlap = CreateCommand(
                userId: userId,
                plateNumber: "51P12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);
            var resOverlap = await mediator.Send(cmdOverlap);
            Assert.False(resOverlap.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, resOverlap.Error.Code);

            // 4. User attempts valid 3rd reservation with "51R-123.45" at 2h -> SUCCEEDS (count becomes 3)
            var cmd3 = CreateCommand(
                userId: userId,
                plateNumber: "51R-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);
            var res3 = await mediator.Send(cmd3);
            Assert.True(res3.IsSuccess);

            // 5. User attempts 4th reservation with "51S-123.45" -> REJECTED by UserActiveReservationsLimitExceeded
            var cmd4 = CreateCommand(
                userId: userId,
                plateNumber: "51S-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);
            var res4 = await mediator.Send(cmd4);
            Assert.False(res4.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.UserActiveReservationsLimitExceeded.Code, res4.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // FIX2-18: Gửi 51Z-123.45 khi đã có đặt chỗ hiệu lực 51Z12345 chồng khung -> xung đột biển số
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-18")]
    public async Task FIX2_18_Incoming_formatted_plate_conflicts_with_existing_unformatted_plate()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            // 1. Existing reservation with unformatted plate "51Z12345"
            var cmd1 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51Z12345",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res1 = await mediator.Send(cmd1);
            Assert.True(res1.IsSuccess);

            // 2. Incoming reservation with formatted plate "51Z-123.45" overlapping window
            var cmd2 = CreateCommand(
                userId: Guid.NewGuid(),
                plateNumber: "51Z-123.45",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res2 = await mediator.Send(cmd2);
            Assert.False(res2.IsSuccess);
            Assert.Equal(ReservationCapacityErrors.PlateReservationOverlap.Code, res2.Error.Code);
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }

    // =========================================================================
    // FIX2-19: Giữ chỗ thành công -> DB lưu biển số canonical và VehicleId từ Identity (khác VehicleId client gửi)
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-19")]
    public async Task FIX2_19_Hold_success_persists_canonical_plate_and_identity_vehicle_id()
    {
        await TestExecutionLock.WaitAsync();
        try
        {
            var (provider, _, _) = CreateTestEnvironment();
            await EnsureMigratedAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

            var clientSuppliedVehicleId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var cmd = CreateCommand(
                userId: userId,
                vehicleId: clientSuppliedVehicleId,
                plateNumber: "51A-999.99",
                appointmentTime: BaseTime.AddHours(2),
                durationMinutes: 60);

            var res = await mediator.Send(cmd);
            Assert.True(res.IsSuccess);

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var dbContext = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var savedReservation = await dbContext.Reservations.AsNoTracking().SingleAsync(r => r.Id == res.Value.Id);

                Assert.Equal("51A99999", savedReservation.PlateNumber);
                Assert.NotEqual(clientSuppliedVehicleId, savedReservation.VehicleId);
            }
        }
        finally
        {
            TestExecutionLock.Release();
        }
    }
}
