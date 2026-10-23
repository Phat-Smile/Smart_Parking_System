extern alias BookingWebApi;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Parking;
using SmartParking.Contracts.Parking.Availability.V1;
using Xunit;

using BookingGrpcClient = BookingWebApi::SmartParking.Contracts.Parking.Availability.V1.ParkingAvailabilityContract.ParkingAvailabilityContractClient;
using BookingParkingAvailabilityGrpcClient = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingAvailabilityGrpcClient;
using BookingParkingClientRegistration = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingClientRegistration;
using BookingParkingServiceOptions = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ParkingServiceOptions;

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

    [Fact]
    public async Task Service_key_header_is_transmitted_and_accepted_by_server_with_security_middleware()
    {
        const string serverKey = "shared-secret-key-123456";

        await using var host = await FakeGrpcServer.StartAsync(
            (req, ctx) => Task.FromResult(new GetAvailableSlotCountResponse { AvailableSlotCount = 8 }),
            serverServiceKey: serverKey);

        // Client with matching service key
        var client = host.CreateAvailabilityClient(clientServiceKey: serverKey);

        var result = await client.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value);
    }

    [Fact]
    public async Task Missing_or_invalid_service_key_is_rejected_by_server_with_security_middleware()
    {
        const string serverKey = "shared-secret-key-123456";

        await using var host = await FakeGrpcServer.StartAsync(
            (req, ctx) => Task.FromResult(new GetAvailableSlotCountResponse { AvailableSlotCount = 8 }),
            serverServiceKey: serverKey);

        // Client with wrong service key
        var clientWrongKey = host.CreateAvailabilityClient(clientServiceKey: "wrong-key-000000");
        var resultWrong = await clientWrongKey.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(resultWrong.IsFailure);

        // Client with no service key
        var clientNoKey = host.CreateAvailabilityClient(clientServiceKey: null);
        var resultNoKey = await clientNoKey.GetAvailableSlotCountAsync(ApprovedLotId, TestContext.Current.CancellationToken);

        Assert.True(resultNoKey.IsFailure);
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
        BookingParkingClientRegistration.AddParkingServiceClient(services, configuration);

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
        BookingParkingClientRegistration.AddParkingServiceClient(services, configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetService<IParkingAvailabilityClient>();
        Assert.NotNull(client);
        Assert.IsType<BookingParkingAvailabilityGrpcClient>(client);
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

        public IParkingAvailabilityClient CreateAvailabilityClient(int timeoutSeconds = 5, string? clientServiceKey = null)
        {
            var grpcClient = new BookingGrpcClient(_channel);
            var options = Options.Create(new BookingParkingServiceOptions { TimeoutSeconds = timeoutSeconds });
            var serviceKeyOpts = Options.Create(new ServiceKeyOptions { ServiceKey = clientServiceKey ?? string.Empty });
            var logger = LoggerFactory.Create(builder => { }).CreateLogger<BookingParkingAvailabilityGrpcClient>();
            return new BookingParkingAvailabilityGrpcClient(grpcClient, options, serviceKeyOpts, logger);
        }

        public static async Task<FakeGrpcServer> StartAsync(
            Func<GetAvailableSlotCountRequest, ServerCallContext, Task<GetAvailableSlotCountResponse>> handler,
            string? serverServiceKey = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakeAvailabilityService(handler));

            if (!string.IsNullOrEmpty(serverServiceKey))
            {
                builder.Configuration[ServiceKeyOptions.SectionName + ":ServiceKey"] = serverServiceKey;
                builder.Services.AddOptions<ServiceKeyOptions>()
                    .Bind(builder.Configuration.GetSection(ServiceKeyOptions.SectionName));
            }

            var app = builder.Build();

            if (!string.IsNullOrEmpty(serverServiceKey))
            {
                app.UseMiddleware<ServiceKeyValidationMiddleware>();
            }

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
