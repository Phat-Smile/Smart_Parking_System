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
using ParkingSystem.Booking.Application.Vehicles;
using SmartParking.Contracts.Identity.Vehicle.V1;
using Xunit;

using BookingIdentityGrpcClient = BookingWebApi::SmartParking.Contracts.Identity.Vehicle.V1.IdentityVehicleContract.IdentityVehicleContractClient;
using BookingIdentityVehicleGrpcClient = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.IdentityVehicleGrpcClient;
using BookingIdentityClientRegistration = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.IdentityClientRegistration;
using BookingIdentityServiceOptions = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.IdentityServiceOptions;

namespace ParkingSystem.Tests.Booking.IntegrationTests.Grpc;

public sealed class IdentityVehicleGrpcClientTests
{
    private static readonly Guid ValidUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ValidVehicleId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private const string ValidPlate = "51A-123.45";
    private const string ValidCanonicalPlate = "51A12345";

    [Fact]
    public async Task Registered_car_owned_by_active_user_returns_success()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = ValidUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Car,
                IsAccountActive = true
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ValidUserId, result.Value.AccountId);
        Assert.Equal(ValidVehicleId, result.Value.VehicleId);
        Assert.Equal(ValidCanonicalPlate, result.Value.CanonicalPlate);
        Assert.Equal(SupportedVehicleType.Car, result.Value.VehicleType);
        Assert.True(result.Value.IsAccountActive);
    }

    [Fact]
    public async Task Empty_plate_returns_empty_plate_error_without_wire_call()
    {
        var wasCalled = false;
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
        {
            wasCalled = true;
            return Task.FromResult(new LookupVehicleResponse { IsRegistered = true });
        });
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync("   ", ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.EmptyPlate, result.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task Invalid_syntax_plate_returns_invalid_plate_format_error_without_wire_call()
    {
        var wasCalled = false;
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
        {
            wasCalled = true;
            return Task.FromResult(new LookupVehicleResponse { IsRegistered = true });
        });
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync("INVALID@PLATE", ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.InvalidPlateFormat, result.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task Empty_expected_user_id_returns_ownership_mismatch_without_wire_call()
    {
        var wasCalled = false;
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
        {
            wasCalled = true;
            return Task.FromResult(new LookupVehicleResponse { IsRegistered = true });
        });
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, Guid.Empty, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch, result.Error);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task Unregistered_plate_returns_vehicle_not_registered()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = false,
                VehicleType = VehicleType.Unspecified
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleNotRegistered, result.Error);
    }

    [Fact]
    public async Task Ownership_mismatch_returns_vehicle_ownership_mismatch()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = OtherUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Car,
                IsAccountActive = true
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch, result.Error);
    }

    [Fact]
    public async Task Motorbike_returns_unsupported_vehicle_type()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = ValidUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Motorbike,
                IsAccountActive = true
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.UnsupportedVehicleType, result.Error);
    }

    [Fact]
    public async Task Unspecified_vehicle_type_returns_unsupported_vehicle_type()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = ValidUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Unspecified,
                IsAccountActive = true
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.UnsupportedVehicleType, result.Error);
    }

    [Fact]
    public async Task Inactive_account_returns_account_not_eligible()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = ValidUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Car,
                IsAccountActive = false
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.AccountNotEligible, result.Error);
    }

    [Fact]
    public async Task Corrupted_guid_in_response_returns_communication_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = "not-a-guid",
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Car,
                IsAccountActive = true
            }));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.CommunicationFailed, result.Error);
    }

    [Fact]
    public async Task Invalid_argument_status_returns_invalid_plate_format()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid plate format.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.InvalidPlateFormat, result.Error);
    }

    [Fact]
    public async Task Unauthenticated_status_returns_service_auth_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing credentials.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.ServiceAuthFailed, result.Error);
    }

    [Fact]
    public async Task Permission_denied_status_returns_service_auth_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Invalid service key.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.ServiceAuthFailed, result.Error);
    }

    [Fact]
    public async Task Deadline_exceeded_returns_deadline_exceeded_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.DeadlineExceeded, result.Error);
    }

    [Fact]
    public async Task Unavailable_status_returns_service_unavailable_error()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.ServiceUnavailable, result.Error);
    }

    [Fact]
    public async Task Cancelled_status_without_caller_token_returns_request_cancelled()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Cancelled, "Call cancelled.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.RequestCancelled, result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_throws_operation_canceled_exception()
    {
        await using var host = await FakeGrpcServer.StartAsync(async (req, ctx) =>
        {
            await Task.Delay(5000, ctx.CancellationToken);
            return new LookupVehicleResponse { IsRegistered = true };
        });
        var client = host.CreateClient();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.VerifyOwnershipAsync(ValidPlate, ValidUserId, cts.Token));
    }

    [Fact]
    public async Task Internal_error_returns_communication_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
            throw new RpcException(new Status(StatusCode.Internal, "Internal crash.")));
        var client = host.CreateClient();

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.CommunicationFailed, result.Error);
    }

    [Fact]
    public async Task Service_key_header_is_sent_when_configured()
    {
        string? receivedKey = null;
        await using var host = await FakeGrpcServer.StartAsync((req, ctx) =>
        {
            receivedKey = ctx.RequestHeaders.GetValue(ServiceKeyValidationMiddleware.HeaderName);
            return Task.FromResult(new LookupVehicleResponse
            {
                IsRegistered = true,
                AccountId = ValidUserId.ToString("D"),
                VehicleId = ValidVehicleId.ToString("D"),
                VehicleType = VehicleType.Car,
                IsAccountActive = true
            });
        });
        var client = host.CreateClient(clientServiceKey: "booking-secret-key-123");

        var result = await client.VerifyOwnershipAsync(ValidPlate, ValidUserId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("booking-secret-key-123", receivedKey);
    }

    [Fact]
    public async Task DI_registration_resolves_IVehicleOwnershipClient_as_IdentityVehicleGrpcClient()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityService:GrpcEndpoint"] = "http://identity-api:8081",
                ["IdentityService:TimeoutSeconds"] = "5"
            })
            .Build();

        services.AddLogging();
        BookingIdentityClientRegistration.AddIdentityServiceClient(services, config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetService<IVehicleOwnershipClient>();
        Assert.NotNull(client);
        Assert.IsType<BookingIdentityVehicleGrpcClient>(client);
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

        public IVehicleOwnershipClient CreateClient(int timeoutSeconds = 5, string? clientServiceKey = null)
        {
            var grpcClient = new BookingIdentityGrpcClient(_channel);
            var options = Options.Create(new BookingIdentityServiceOptions { TimeoutSeconds = timeoutSeconds });
            var serviceKeyOpts = Options.Create(new ServiceKeyOptions { ServiceKey = clientServiceKey ?? string.Empty });
            var logger = LoggerFactory.Create(builder => { }).CreateLogger<BookingIdentityVehicleGrpcClient>();
            return new BookingIdentityVehicleGrpcClient(grpcClient, options, serviceKeyOpts, logger);
        }

        public static async Task<FakeGrpcServer> StartAsync(
            Func<LookupVehicleRequest, ServerCallContext, Task<LookupVehicleResponse>> handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakeIdentityVehicleService(handler));

            var app = builder.Build();
            app.MapGrpcService<FakeIdentityVehicleService>();
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

    private sealed class FakeIdentityVehicleService(
        Func<LookupVehicleRequest, ServerCallContext, Task<LookupVehicleResponse>> handler)
        : IdentityVehicleContract.IdentityVehicleContractBase
    {
        public override Task<LookupVehicleResponse> LookupVehicle(
            LookupVehicleRequest request,
            ServerCallContext context) => handler(request, context);
    }
}
