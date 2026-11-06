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
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using SmartParking.Contracts.Parking.Availability.V1;
using Xunit;
using ParkingAvailabilityGrpcService = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.ParkingAvailabilityGrpcService;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Contract 2 over an in-memory TestServer: no Docker, no physical port. The client is generated from the shared
// proto the same way Booking will generate it, so these tests also guard the wire contract.
public sealed class ParkingAvailabilityGrpcTests
{
    private const string ApprovedLotId = "10000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task Approved_lot_returns_its_available_car_slot_count()
    {
        await using var host = await GrpcHost.StartAsync(availableSlots: 7);

        var response = await host.Client.GetAvailableSlotCountAsync(
            new GetAvailableSlotCountRequest { LotId = ApprovedLotId },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(7, response.AvailableSlotCount);
    }

    [Fact]
    public async Task Full_lot_returns_zero()
    {
        await using var host = await GrpcHost.StartAsync(availableSlots: 0);

        var response = await host.Client.GetAvailableSlotCountAsync(
            new GetAvailableSlotCountRequest { LotId = ApprovedLotId },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, response.AvailableSlotCount);
    }

    [Fact]
    public async Task Unknown_or_unapproved_lot_returns_not_found()
    {
        await using var host = await GrpcHost.StartAsync(availableSlots: 7);

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetAvailableSlotCountAsync(
            new GetAvailableSlotCountRequest { LotId = Guid.NewGuid().ToString("D") },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("10000000000000000000000000000001")]
    public async Task Malformed_lot_id_returns_invalid_argument(string lotId)
    {
        await using var host = await GrpcHost.StartAsync(availableSlots: 7);

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetAvailableSlotCountAsync(
            new GetAvailableSlotCountRequest { LotId = lotId },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
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

        public static async Task<GrpcHost> StartAsync(int availableSlots)
        {
            var builder = WebApplication.CreateBuilder();
            // This gRPC contract host supplies only the readers used by the availability handler.
            // Do not validate unrelated application handlers whose repositories are not registered here.
            builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddApplicationServices();
            builder.Services.AddSingleton<IParkingLotDetailReader>(new FakeLotReader());
            builder.Services.AddSingleton<IAvailableSlotReader>(new FixedAvailableSlotReader(availableSlots));

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

    // Only ApprovedLotId is an approved lot.
    private sealed class FakeLotReader : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == Guid.Parse(ApprovedLotId)
                ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Demo Street", 10.77m, 106.70m, "Outdoor")
                : null);
    }

    private sealed class FixedAvailableSlotReader(int count) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(count);
    }
}
