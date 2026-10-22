using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.WebAPI.Grpc;
using SmartParking.Contracts.Parking.Availability.V1;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests.Grpc;

public sealed class ParkingAvailabilityGrpcClientTests
{
    private static readonly Guid ApprovedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UnknownLotId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Approved_lot_returns_available_slot_count()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new GetAvailableSlotCountResponse { AvailableSlotCount = 12 }));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value);
    }

    [Fact]
    public async Task Empty_lot_id_returns_empty_lot_id_error_without_wire_call()
    {
        var wasCalled = false;
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
        {
            wasCalled = true;
            return Task.FromResult(new GetAvailableSlotCountResponse { AvailableSlotCount = 5 });
        });
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(Guid.Empty, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.EmptyLotId, result.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task Unknown_lot_returns_lot_not_found_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.NotFound, "Lot not found.")));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(UnknownLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.LotNotFound, result.Error);
    }

    [Fact]
    public async Task Negative_count_returns_invalid_response_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new GetAvailableSlotCountResponse { AvailableSlotCount = -5 }));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.InvalidResponse, result.Error);
    }

    [Fact]
    public async Task Deadline_exceeded_returns_deadline_exceeded_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded.")));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.DeadlineExceeded, result.Error);
    }

    [Fact]
    public async Task Cancelled_status_returns_request_cancelled_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Cancelled, "Call cancelled.")));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.RequestCancelled, result.Error);
    }

    [Fact]
    public async Task Cancelled_cancellation_token_returns_request_cancelled_error()
    {
        await using var host = await FakeGrpcServer.StartAsync(async (req, ctx) =>
        {
            await Task.Delay(500, ctx.CancellationToken);
            return new GetAvailableSlotCountResponse { AvailableSlotCount = 5 };
        });
        var client = host.CreateAvailabilityClient();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, cts.Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.RequestCancelled, result.Error);
    }

    [Fact]
    public async Task Service_unavailable_returns_service_unavailable_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable.")));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.ServiceUnavailable, result.Error);
    }

    [Fact]
    public async Task Internal_server_error_returns_communication_failed_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Internal, "Internal failure.")));
        var client = host.CreateAvailabilityClient();

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingAvailabilityErrors.CommunicationFailed, result.Error);
    }

    [Theory]
    [InlineData("", 5, "ParkingService:GrpcEndpoint must be a valid absolute URI.")]
    [InlineData("not-a-uri", 5, "ParkingService:GrpcEndpoint must be a valid absolute URI.")]
    [InlineData("http://parking-api:8081", 0, "ParkingService:TimeoutSeconds must be greater than 0.")]
    [InlineData("http://parking-api:8081", -1, "ParkingService:TimeoutSeconds must be greater than 0.")]
    public void AddParkingServiceClient_validates_configuration_on_start(string endpoint, int timeout, string expectedMessage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParkingService:GrpcEndpoint"] = endpoint,
                ["ParkingService:TimeoutSeconds"] = timeout.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParkingServiceClient(configuration);

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IParkingAvailabilityClient>());
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void AddParkingServiceClient_registers_and_resolves_IParkingAvailabilityClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParkingService:GrpcEndpoint"] = "http://localhost:5002",
                ["ParkingService:TimeoutSeconds"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParkingServiceClient(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetService<IParkingAvailabilityClient>();
        Assert.NotNull(client);
        Assert.IsType<ParkingAvailabilityGrpcClient>(client);
    }

    private sealed class FakeGrpcServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        private FakeGrpcServer(WebApplication app, GrpcChannel channel)
        {
            _app = app;
            _channel = channel;
        }

        public IParkingAvailabilityClient CreateAvailabilityClient(int timeoutSeconds = 5)
        {
            var grpcClient = new ParkingAvailabilityContract.ParkingAvailabilityContractClient(_channel);
            var options = Options.Create(new ParkingServiceOptions { TimeoutSeconds = timeoutSeconds });
            var logger = LoggerFactory.Create(builder => { }).CreateLogger<ParkingAvailabilityGrpcClient>();
            return new ParkingAvailabilityGrpcClient(grpcClient, options, logger);
        }

        public static async Task<FakeGrpcServer> StartAsync(
            Func<GetAvailableSlotCountRequest, ServerCallContext, Task<GetAvailableSlotCountResponse>> handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakeAvailabilityService(handler));

            var app = builder.Build();
            app.MapGrpcService<FakeAvailabilityService>();
            await app.StartAsync(TestContext.Current.CancellationToken);

            var server = app.GetTestServer();
            var channel = GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions
            {
                HttpHandler = server.CreateHandler()
            });

            return new FakeGrpcServer(app, channel);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _app.DisposeAsync();
        }
    }

    private sealed class FakeAvailabilityService(
        Func<GetAvailableSlotCountRequest, ServerCallContext, Task<GetAvailableSlotCountResponse>> handler)
        : ParkingAvailabilityContract.ParkingAvailabilityContractBase
    {
        public override Task<GetAvailableSlotCountResponse> GetAvailableSlotCount(
            GetAvailableSlotCountRequest request,
            ServerCallContext context) => handler(request, context);
    }
}
