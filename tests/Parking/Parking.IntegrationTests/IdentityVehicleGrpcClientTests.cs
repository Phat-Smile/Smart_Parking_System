extern alias ParkingWebApi;

using System.Collections.Concurrent;
using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.Common.Identity;
using SmartParking.Contracts.Identity.Vehicle.V1;
using Xunit;
using IdentityClient = ParkingWebApi::SmartParking.Contracts.Identity.Vehicle.V1.IdentityVehicleContract.IdentityVehicleContractClient;
using IdentityServiceOptions = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.IdentityServiceOptions;
using IdentityVehicleGrpcClient = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.IdentityVehicleGrpcClient;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Contract 1 (plate -> account and vehicle) seen from Parking, over an in-memory TestServer: no Docker, no port.
// The fake Identity is generated from the shared proto, so these tests also guard the wire contract.
public sealed class IdentityVehicleGrpcClientTests
{
    private const string Plate = "51F12345";
    private static readonly Guid AccountId = Guid.Parse("a3bb108e-17d4-4a6c-9f87-2a54e9912345");
    private static readonly Guid VehicleId = Guid.Parse("c9b578e2-f472-4b2a-8bc2-a1b948f93e12");

    [Fact]
    public async Task Registered_vehicle_of_an_active_account_links_the_session()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Equal(new PlateOwner(AccountId, VehicleId), owner);
    }

    [Fact]
    public async Task The_plate_reaches_identity_as_given()
    {
        string? received = null;
        await using var identity = await FakeIdentity.StartAsync((request, _) =>
        {
            received = request.LicensePlate;
            return Task.FromResult(Registered());
        });
        var lookup = identity.CreateLookup();

        await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Equal(Plate, received);
    }

    [Fact]
    public async Task Plate_of_no_registered_vehicle_is_not_linked()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(new LookupVehicleResponse { IsRegistered = false }));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(1, identity.Calls);
    }

    [Fact]
    public async Task Vehicle_of_an_account_that_is_not_active_is_not_linked()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered(active: false)));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(1, identity.Calls);
    }

    [Theory]
    [InlineData("", "c9b578e2-f472-4b2a-8bc2-a1b948f93e12")]
    [InlineData("a3bb108e-17d4-4a6c-9f87-2a54e9912345", "")]
    [InlineData("not-a-guid", "c9b578e2-f472-4b2a-8bc2-a1b948f93e12")]
    [InlineData("a3bb108e-17d4-4a6c-9f87-2a54e9912345", "not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000", "c9b578e2-f472-4b2a-8bc2-a1b948f93e12")]
    [InlineData("a3bb108e-17d4-4a6c-9f87-2a54e9912345", "00000000-0000-0000-0000-000000000000")]
    public async Task Registered_answer_without_usable_identifiers_is_not_linked(string accountId, string vehicleId)
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered(accountId: accountId, vehicleId: vehicleId)));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(1, identity.Calls);
    }

    [Theory]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unknown)]
    public async Task Identity_failure_never_blocks_the_gate(StatusCode status)
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => throw new RpcException(new Status(status, "boom")));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(1, identity.Calls);
    }

    [Fact]
    public async Task A_failed_lookup_does_not_poison_the_next_one()
    {
        var calls = 0;
        await using var identity = await FakeIdentity.StartAsync((_, _) =>
            ++calls == 1
                ? throw new RpcException(new Status(StatusCode.Unavailable, "restarting"))
                : Task.FromResult(Registered()));
        var lookup = identity.CreateLookup();

        var first = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);
        var second = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(first);
        Assert.Equal(new PlateOwner(AccountId, VehicleId), second);
    }

    [Fact]
    public async Task A_slow_identity_is_cut_at_the_deadline_and_the_session_is_not_linked()
    {
        await using var identity = await FakeIdentity.StartAsync(async (_, context) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), context.CancellationToken);
            return Registered();
        });
        var lookup = identity.CreateLookup(timeoutSeconds: 1);

        var stopwatch = Stopwatch.StartNew();
        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Null(owner);
        Assert.Equal(1, identity.Calls);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_cancelled_request_is_not_swallowed()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()));
        var lookup = identity.CreateLookup();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.FindByPlateAsync(Plate, cancelled.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_plate_is_not_sent_to_identity(string plate)
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()));
        var lookup = identity.CreateLookup();

        var owner = await lookup.FindByPlateAsync(plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(0, identity.Calls);
    }

    [Fact]
    public async Task The_service_key_is_sent_when_configured()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()), serverServiceKey: "internal-key");
        var lookup = identity.CreateLookup(serviceKey: "internal-key");

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Equal(new PlateOwner(AccountId, VehicleId), owner);
        Assert.Equal("internal-key", identity.LastServiceKey);
    }

    [Fact]
    public async Task No_service_key_header_is_sent_when_none_is_configured()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()));
        var lookup = identity.CreateLookup(serviceKey: null);

        await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Equal(1, identity.Calls);
        Assert.Null(identity.LastServiceKey);
    }

    [Fact]
    public async Task A_wrong_service_key_leaves_the_session_unlinked_instead_of_failing_it()
    {
        await using var identity = await FakeIdentity.StartAsync((_, _) => Task.FromResult(Registered()), serverServiceKey: "internal-key");
        var lookup = identity.CreateLookup(serviceKey: "wrong-key");

        var owner = await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.Null(owner);
        Assert.Equal(0, identity.Calls);
    }

    [Theory]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Internal)]
    public async Task Failures_are_logged_without_the_plate(StatusCode status)
    {
        var logs = new CapturedLogs();
        await using var identity = await FakeIdentity.StartAsync((_, _) => throw new RpcException(new Status(status, $"plate {Plate} exploded")));
        var lookup = identity.CreateLookup(logs: logs);

        await lookup.FindByPlateAsync(Plate, TestContext.Current.CancellationToken);

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(Plate, StringComparison.OrdinalIgnoreCase));
    }

    private static LookupVehicleResponse Registered(bool active = true, string? accountId = null, string? vehicleId = null) => new()
    {
        IsRegistered = true,
        AccountId = accountId ?? AccountId.ToString("D"),
        VehicleId = vehicleId ?? VehicleId.ToString("D"),
        VehicleType = VehicleType.Car,
        IsAccountActive = active
    };

    private sealed class CallLog
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public string? LastServiceKey { get; private set; }

        public void Record(ServerCallContext context)
        {
            LastServiceKey = context.RequestHeaders.GetValue(ServiceKeyValidationMiddleware.HeaderName.ToLowerInvariant());
            Interlocked.Increment(ref _calls);
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturedLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }

    private sealed class FakeIdentityService(
        Func<LookupVehicleRequest, ServerCallContext, Task<LookupVehicleResponse>> handler,
        CallLog log) : IdentityVehicleContract.IdentityVehicleContractBase
    {
        public override Task<LookupVehicleResponse> LookupVehicle(LookupVehicleRequest request, ServerCallContext context)
        {
            log.Record(context);
            return handler(request, context);
        }
    }

    private sealed class FakeIdentity : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;
        private readonly CallLog _log;

        private FakeIdentity(WebApplication app, GrpcChannel channel, CallLog log)
        {
            _app = app;
            _channel = channel;
            _log = log;
        }

        public int Calls => _log.Calls;

        public string? LastServiceKey => _log.LastServiceKey;

        public IPlateOwnerLookup CreateLookup(int timeoutSeconds = 2, string? serviceKey = null, CapturedLogs? logs = null)
        {
            var options = Options.Create(new IdentityServiceOptions { GrpcEndpoint = "http://identity.test", TimeoutSeconds = timeoutSeconds });
            var serviceKeyOptions = Options.Create(new ServiceKeyOptions { ServiceKey = serviceKey ?? string.Empty });
            var logger = LoggerFactory
                .Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs ?? new CapturedLogs()))
                .CreateLogger<IdentityVehicleGrpcClient>();
            return new IdentityVehicleGrpcClient(new IdentityClient(_channel), options, serviceKeyOptions, logger);
        }

        public static async Task<FakeIdentity> StartAsync(
            Func<LookupVehicleRequest, ServerCallContext, Task<LookupVehicleResponse>> handler,
            string? serverServiceKey = null)
        {
            var log = new CallLog();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakeIdentityService(handler, log));

            if (!string.IsNullOrEmpty(serverServiceKey))
            {
                builder.Configuration[ServiceKeyOptions.SectionName + ":ServiceKey"] = serverServiceKey;
                builder.Services.AddOptions<ServiceKeyOptions>()
                    .Bind(builder.Configuration.GetSection(ServiceKeyOptions.SectionName));
            }

            var app = builder.Build();
            if (!string.IsNullOrEmpty(serverServiceKey))
                app.UseMiddleware<ServiceKeyValidationMiddleware>();

            app.MapGrpcService<FakeIdentityService>();
            await app.StartAsync(TestContext.Current.CancellationToken);

            var server = app.GetTestServer();
            var channel = GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions { HttpHandler = server.CreateHandler() });
            return new FakeIdentity(app, channel, log);
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _app.DisposeAsync();
        }
    }
}
