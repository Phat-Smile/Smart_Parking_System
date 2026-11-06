extern alias BookingWebApi;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using SmartParking.Contracts.Parking.Availability.V1;
using Xunit;

using BookingGrpcClient = BookingWebApi::SmartParking.Contracts.Parking.Availability.V1.ParkingAvailabilityContract.ParkingAvailabilityContractClient;
using BookingParkingClientRegistration = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingClientRegistration;
using BookingParkingServiceOptions = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingServiceOptions;
using CapacityGrpcProvider = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingLotCapacityGrpcProvider;
using PricingGrpcProvider = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingPricingGrpcProvider;

namespace ParkingSystem.Tests.Booking.IntegrationTests.Grpc;

// P0-A: the real Booking providers talk to a Parking-shaped gRPC server over an in-memory TestServer (no Docker).
public sealed class ParkingReservationGrpcProvidersTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.FromHours(7));

    // ---- capacity ----

    [Fact]
    public async Task Capacity_maps_every_field_of_the_contract()
    {
        await using var host = await FakeParkingServer.StartAsync(info: _ => Task.FromResult(new GetLotReservationInfoResponse
        {
            ValidCapacity = 7,
            ReservationRatioPercent = 40,
            AvailableSlotCount = 4,
            ReservationEnabled = true,
            DepositAmount = 25_000
        }));

        var result = await host.CapacityProvider().GetCapacityInfoAsync(LotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ParkingLotCapacityInfo(7, 40, 4, true, 25_000m), result.Value);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.PermissionDenied)]
    public async Task Capacity_failures_fail_closed(StatusCode code)
    {
        await using var host = await FakeParkingServer.StartAsync(
            info: _ => throw new RpcException(new Status(code, "boom")));

        var result = await host.CapacityProvider().GetCapacityInfoAsync(LotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
    }

    [Theory]
    [InlineData(-1, 50, 3, 20_000)]
    [InlineData(10, 101, 3, 20_000)]
    [InlineData(10, 50, -1, 20_000)]
    [InlineData(10, 50, 3, -1)]
    public async Task Abnormal_capacity_figures_are_refused(int capacity, int ratio, int available, long deposit)
    {
        await using var host = await FakeParkingServer.StartAsync(info: _ => Task.FromResult(new GetLotReservationInfoResponse
        {
            ValidCapacity = capacity,
            ReservationRatioPercent = ratio,
            AvailableSlotCount = available,
            ReservationEnabled = true,
            DepositAmount = deposit
        }));

        var result = await host.CapacityProvider().GetCapacityInfoAsync(LotId, TestContext.Current.CancellationToken);

        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
    }

    [Fact]
    public async Task Empty_lot_id_is_refused_without_a_wire_call()
    {
        var called = false;
        await using var host = await FakeParkingServer.StartAsync(info: _ =>
        {
            called = true;
            return Task.FromResult(new GetLotReservationInfoResponse());
        });

        var result = await host.CapacityProvider().GetCapacityInfoAsync(Guid.Empty, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.False(called);
    }

    // ---- pricing ----

    [Fact]
    public async Task Pricing_maps_a_block_price_list_and_sends_the_vehicle_group()
    {
        GetLotPricingRequest? seen = null;
        await using var host = await FakeParkingServer.StartAsync(pricing: request =>
        {
            seen = request;
            return Task.FromResult(new GetLotPricingResponse
            {
                Price = 12_000, BillingMode = "Block", BlockMinutes = 30, OvernightFee = 30_000, PolicyVersion = 2
            });
        });

        var result = await host.PricingProvider().GetPricingInfoAsync(LotId, VehicleGroup.Car, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(LotId.ToString("D"), seen!.LotId);
        Assert.Equal("Car", seen.VehicleGroup);
        var info = result.Value!;
        Assert.Equal(12_000m, info.Price);
        Assert.Equal("Block", info.BillingMode);
        Assert.Equal(30, info.BlockMinutes);
        Assert.Equal(30_000m, info.OvernightFee);
        Assert.Equal("VND", info.Currency);
        Assert.Equal(2, info.PolicyVersion);
        Assert.Equal(VehicleGroup.Car, info.VehicleGroup);
    }

    [Fact]
    public async Task Pricing_maps_per_entry_to_no_block_minutes()
    {
        await using var host = await FakeParkingServer.StartAsync(pricing: _ => Task.FromResult(new GetLotPricingResponse
        {
            Price = 5_000, BillingMode = "PerEntry", BlockMinutes = 0, OvernightFee = 0, PolicyVersion = 1
        }));

        var result = await host.PricingProvider().GetPricingInfoAsync(LotId, VehicleGroup.Motorbike, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("PerEntry", result.Value!.BillingMode);
        Assert.Null(result.Value.BlockMinutes);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.InvalidArgument)]
    public async Task Pricing_failures_fail_closed(StatusCode code)
    {
        await using var host = await FakeParkingServer.StartAsync(
            pricing: _ => throw new RpcException(new Status(code, "boom")));

        var result = await host.PricingProvider().GetPricingInfoAsync(LotId, VehicleGroup.Car, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
    }

    [Theory]
    [InlineData(0L, "Block", 60, 0L)]
    [InlineData(-5L, "Block", 60, 0L)]
    [InlineData(10_000L, "Flat", 60, 0L)]
    [InlineData(10_000L, "", 60, 0L)]
    [InlineData(10_000L, "Block", -1, 0L)]
    [InlineData(10_000L, "Block", 60, -1L)]
    public async Task Abnormal_price_lists_are_refused(long price, string mode, int block, long overnight)
    {
        await using var host = await FakeParkingServer.StartAsync(pricing: _ => Task.FromResult(new GetLotPricingResponse
        {
            Price = price, BillingMode = mode, BlockMinutes = block, OvernightFee = overnight, PolicyVersion = 1
        }));

        var result = await host.PricingProvider().GetPricingInfoAsync(LotId, VehicleGroup.Car, TestContext.Current.CancellationToken);

        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
    }

    // ---- transport ----

    [Fact]
    public async Task Service_key_is_transmitted_to_parking()
    {
        const string key = "shared-secret-key-123456";
        await using var host = await FakeParkingServer.StartAsync(
            info: _ => Task.FromResult(new GetLotReservationInfoResponse { ValidCapacity = 1, ReservationEnabled = true }),
            serverServiceKey: key);

        var ok = await host.CapacityProvider(clientServiceKey: key).GetCapacityInfoAsync(LotId, TestContext.Current.CancellationToken);
        var wrong = await host.CapacityProvider(clientServiceKey: "wrong-key-000000").GetCapacityInfoAsync(LotId, TestContext.Current.CancellationToken);

        Assert.True(ok.IsSuccess);
        Assert.True(wrong.IsFailure);
    }

    [Fact]
    public void Registration_replaces_the_unconfigured_providers_with_the_grpc_ones()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParkingService:GrpcEndpoint"] = "http://parking-api:8081",
                ["ParkingService:TimeoutSeconds"] = "5"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<ServiceKeyOptions>();
        BookingParkingClientRegistration.AddParkingServiceClient(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<CapacityGrpcProvider>(scope.ServiceProvider.GetRequiredService<IParkingLotCapacityProvider>());
        Assert.IsType<PricingGrpcProvider>(scope.ServiceProvider.GetRequiredService<IParkingPricingProvider>());
    }

    // ---- end to end: from the create-hold command to a reservation, no fake provider ----

    [Fact]
    public async Task Scheduled_hold_reaches_the_coordinator_with_the_prepayment_computed_from_the_parking_price_list()
    {
        await using var host = await FakeParkingServer.StartAsync(
            info: _ => Task.FromResult(new GetLotReservationInfoResponse
            {
                ValidCapacity = 10, ReservationRatioPercent = 50, AvailableSlotCount = 5,
                ReservationEnabled = true, DepositAmount = 20_000
            }),
            pricing: _ => Task.FromResult(new GetLotPricingResponse
            {
                Price = 10_000, BillingMode = "Block", BlockMinutes = 60, OvernightFee = 0, PolicyVersion = 1
            }));
        var coordinator = new CapturingCoordinator();
        var handler = new CreateTemporaryHoldCommandHandler(
            new OwnerOfEverything(),
            host.CapacityProvider(),
            coordinator,
            new FixedClock(Now),
            NullLogger<CreateTemporaryHoldCommandHandler>.Instance,
            host.PricingProvider());

        var result = await handler.Handle(
            new CreateTemporaryHoldCommand(
                ReservationId: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                VehicleId: Guid.NewGuid(),
                PlateNumber: "51A-123.45",
                LotId: LotId,
                VehicleGroup: VehicleGroup.Car,
                ReservationType: ReservationType.Scheduled,
                AppointmentTime: Now.AddHours(2),
                DurationMinutes: 120,
                PrepaidAmount: 1m),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, coordinator.Calls);
        // 120 minutes = 2 blocks of 60 at 10,000: Parking's price list decides the amount, not the client's 1.
        Assert.Equal(20_000m, coordinator.Reservation!.PrepaidAmount);
    }

    [Fact]
    public async Task Hold_is_refused_as_capacity_unavailable_when_parking_cannot_answer()
    {
        await using var host = await FakeParkingServer.StartAsync(
            info: _ => throw new RpcException(new Status(StatusCode.Unavailable, "down")));
        var coordinator = new CapturingCoordinator();
        var handler = new CreateTemporaryHoldCommandHandler(
            new OwnerOfEverything(),
            host.CapacityProvider(),
            coordinator,
            new FixedClock(Now),
            NullLogger<CreateTemporaryHoldCommandHandler>.Instance,
            host.PricingProvider());

        var result = await handler.Handle(
            new CreateTemporaryHoldCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A-123.45", LotId, VehicleGroup.Car,
                ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 0m),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, coordinator.Calls);
    }

    // ---- hosts and fakes ----

    private sealed class FakeParkingServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        private FakeParkingServer(WebApplication app, GrpcChannel channel)
        {
            _app = app;
            _channel = channel;
        }

        public IParkingLotCapacityProvider CapacityProvider(string? clientServiceKey = null) =>
            new CapacityGrpcProvider(
                new BookingGrpcClient(_channel),
                Options.Create(new BookingParkingServiceOptions { TimeoutSeconds = 5 }),
                Options.Create(new ServiceKeyOptions { ServiceKey = clientServiceKey ?? string.Empty }),
                NullLogger<CapacityGrpcProvider>.Instance);

        public IParkingPricingProvider PricingProvider(string? clientServiceKey = null) =>
            new PricingGrpcProvider(
                new BookingGrpcClient(_channel),
                Options.Create(new BookingParkingServiceOptions { TimeoutSeconds = 5 }),
                Options.Create(new ServiceKeyOptions { ServiceKey = clientServiceKey ?? string.Empty }),
                NullLogger<PricingGrpcProvider>.Instance);

        public static async Task<FakeParkingServer> StartAsync(
            Func<GetLotReservationInfoRequest, Task<GetLotReservationInfoResponse>>? info = null,
            Func<GetLotPricingRequest, Task<GetLotPricingResponse>>? pricing = null,
            string? serverServiceKey = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakeParkingService(info, pricing));

            if (!string.IsNullOrEmpty(serverServiceKey))
            {
                builder.Configuration[ServiceKeyOptions.SectionName + ":ServiceKey"] = serverServiceKey;
                builder.Services.AddOptions<ServiceKeyOptions>()
                    .Bind(builder.Configuration.GetSection(ServiceKeyOptions.SectionName));
            }

            var app = builder.Build();
            if (!string.IsNullOrEmpty(serverServiceKey))
                app.UseMiddleware<ServiceKeyValidationMiddleware>();

            app.MapGrpcService<FakeParkingService>();
            await app.StartAsync(TestContext.Current.CancellationToken);

            var server = app.GetTestServer();
            var channel = GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions
            {
                HttpHandler = server.CreateHandler()
            });
            return new FakeParkingServer(app, channel);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _app.DisposeAsync();
        }
    }

    private sealed class FakeParkingService(
        Func<GetLotReservationInfoRequest, Task<GetLotReservationInfoResponse>>? info,
        Func<GetLotPricingRequest, Task<GetLotPricingResponse>>? pricing)
        : ParkingAvailabilityContract.ParkingAvailabilityContractBase
    {
        public override Task<GetLotReservationInfoResponse> GetLotReservationInfo(
            GetLotReservationInfoRequest request, ServerCallContext context) =>
            info is null ? throw new RpcException(new Status(StatusCode.Unimplemented, "not set")) : info(request);

        public override Task<GetLotPricingResponse> GetLotPricing(
            GetLotPricingRequest request, ServerCallContext context) =>
            pricing is null ? throw new RpcException(new Status(StatusCode.Unimplemented, "not set")) : pricing(request);
    }

    private sealed class OwnerOfEverything : IVehicleOwnershipClient
    {
        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
            string rawOrCanonicalPlate, Guid expectedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(expectedUserId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
    }

    private sealed class CapturingCoordinator : IReservationCapacityCoordinator
    {
        public int Calls { get; private set; }
        public Reservation? Reservation { get; private set; }

        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation,
            int ceiling,
            int? availableSlotCount,
            TimeSpan lockTimeout,
            int maxActiveReservationsPerUser = 3,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Reservation = reservation;
            return Task.FromResult(Result<Reservation>.Success(reservation));
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
