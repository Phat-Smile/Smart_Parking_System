extern alias ParkingWebApi;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.Entities;
using SmartParking.Contracts.Parking.Availability.V1;
using Xunit;
using ParkingAvailabilityGrpcService = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.ParkingAvailabilityGrpcService;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// P0-A over an in-memory TestServer: the two Booking-facing rpcs on the wire, no Docker.
public sealed class ParkingReservationGrpcTests
{
    private static readonly Guid ApprovedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Reservation_info_carries_every_field_of_the_contract()
    {
        await using var host = await GrpcHost.StartAsync();

        var response = await host.Client.GetLotReservationInfoAsync(
            new GetLotReservationInfoRequest { LotId = ApprovedLotId.ToString("D") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(7, response.ValidCapacity);
        Assert.Equal(40, response.ReservationRatioPercent);
        Assert.Equal(4, response.AvailableSlotCount);
        Assert.True(response.ReservationEnabled);
        Assert.Equal(25_000L, response.DepositAmount);
    }

    [Fact]
    public async Task Pricing_carries_every_field_of_the_contract()
    {
        await using var host = await GrpcHost.StartAsync();

        var response = await host.Client.GetLotPricingAsync(
            new GetLotPricingRequest { LotId = ApprovedLotId.ToString("D"), VehicleGroup = "Car" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(12_000L, response.Price);
        Assert.Equal("Block", response.BillingMode);
        Assert.Equal(30, response.BlockMinutes);
        Assert.Equal(30_000L, response.OvernightFee);
        Assert.Equal(2, response.PolicyVersion);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("pricing")]
    public async Task Unknown_or_unapproved_lot_is_not_found(string rpc)
    {
        await using var host = await GrpcHost.StartAsync();
        var unknown = Guid.NewGuid().ToString("D");

        var exception = await Assert.ThrowsAsync<RpcException>(() => rpc == "info"
            ? host.Client.GetLotReservationInfoAsync(new GetLotReservationInfoRequest { LotId = unknown },
                cancellationToken: TestContext.Current.CancellationToken).ResponseAsync
            : host.Client.GetLotPricingAsync(new GetLotPricingRequest { LotId = unknown, VehicleGroup = "Car" },
                cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("10000000000000000000000000000001")]
    public async Task Malformed_lot_id_is_invalid_argument(string lotId)
    {
        await using var host = await GrpcHost.StartAsync();

        var info = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetLotReservationInfoAsync(
            new GetLotReservationInfoRequest { LotId = lotId },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);
        var pricing = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetLotPricingAsync(
            new GetLotPricingRequest { LotId = lotId, VehicleGroup = "Car" },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, info.StatusCode);
        Assert.Equal(StatusCode.InvalidArgument, pricing.StatusCode);
    }

    [Fact]
    public async Task Unknown_vehicle_group_is_invalid_argument()
    {
        await using var host = await GrpcHost.StartAsync();

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetLotPricingAsync(
            new GetLotPricingRequest { LotId = ApprovedLotId.ToString("D"), VehicleGroup = "Truck" },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task Missing_price_list_is_failed_precondition()
    {
        await using var host = await GrpcHost.StartAsync();

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetLotPricingAsync(
            new GetLotPricingRequest { LotId = ApprovedLotId.ToString("D"), VehicleGroup = "Motorbike" },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
    }

    private sealed class GrpcHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        private GrpcHost(WebApplication app, GrpcChannel channel)
        {
            _app = app;
            _channel = channel;
            Client = new ParkingAvailabilityContract.ParkingAvailabilityContractClient(channel);
        }

        public ParkingAvailabilityContract.ParkingAvailabilityContractClient Client { get; }

        public static async Task<GrpcHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            // Only the readers used by the two handlers are registered; do not validate unrelated handlers.
            builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddApplicationServices();
            builder.Services.AddSingleton<IParkingLotDetailReader>(new FakeLots());
            builder.Services.AddSingleton<IAvailableSlotReader>(new FakeAvailable());
            builder.Services.AddSingleton<IZoneCapacityReader>(new FakeZones());
            builder.Services.AddSingleton<IReservationRatioPolicyRepository>(new FakeRatio());
            builder.Services.AddSingleton<IReservationDepositPolicyRepository>(new FakeDeposit());
            builder.Services.AddSingleton<IReservationAvailabilityPolicyRepository>(new FakeAvailability());
            builder.Services.AddSingleton<IPricingPolicyRepository>(new FakePricing());

            var app = builder.Build();
            app.MapGrpcService<ParkingAvailabilityGrpcService>();
            await app.StartAsync(TestContext.Current.CancellationToken);

            var server = app.GetTestServer();
            var channel = GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions
            {
                HttpHandler = server.CreateHandler()
            });
            return new GrpcHost(app, channel);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _app.DisposeAsync();
        }
    }

    private sealed class FakeLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == ApprovedLotId
                ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Demo Street", 10.77m, 106.70m, "Outdoor")
                : null);
    }

    private sealed class FakeAvailable : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(4);
    }

    private sealed class FakeZones : IZoneCapacityReader
    {
        public Task<bool> IsOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<FloorCapacity>> ReadAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FloorCapacity>>(
            [
                new FloorCapacity(Guid.NewGuid(), "F1",
                [
                    new ZoneCapacityItem(Guid.NewGuid(), "A", "Car", 5, 5, 4, 0),
                    new ZoneCapacityItem(Guid.NewGuid(), "B", "Car", 3, 3, 3, 0),
                    new ZoneCapacityItem(Guid.NewGuid(), "M", "Motorbike", 20, null, null, 0)
                ])
            ]);
    }

    private sealed class FakeRatio : IReservationRatioPolicyRepository
    {
        public Task<ReservationRatioPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReservationRatioPolicy?>(ReservationRatioPolicy.Create(lotId, 40));
        public Task AddAsync(ReservationRatioPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<ReservationRatioPolicy> UpsertAsync(Guid lotId, int ratioPercent, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDeposit : IReservationDepositPolicyRepository
    {
        public Task<ReservationDepositPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReservationDepositPolicy?>(ReservationDepositPolicy.Create(lotId, 25_000m));
        public Task AddAsync(ReservationDepositPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeAvailability : IReservationAvailabilityPolicyRepository
    {
        public Task<ReservationAvailabilityPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReservationAvailabilityPolicy?>(null);
        public Task AddAsync(ReservationAvailabilityPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<ReservationAvailabilityPolicy> UpsertAsync(Guid lotId, bool reservationEnabled, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePricing : IPricingPolicyRepository
    {
        private static readonly PricingPolicy[] Policies =
        [
            PricingPolicy.Create(ApprovedLotId, "CAR", 1, "Block", 60, 10_000m, 0m),
            PricingPolicy.Create(ApprovedLotId, "CAR", 2, "Block", 30, 12_000m, 30_000m)
        ];

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) =>
            Task.FromResult(Policies
                .Where(p => p.LotId == lotId && p.VehicleType == vehicleType)
                .OrderByDescending(p => p.Version)
                .FirstOrDefault());
        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
