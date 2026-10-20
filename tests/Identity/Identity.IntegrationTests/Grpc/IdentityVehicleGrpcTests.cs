extern alias IdentityWebApi;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.Identity.Application;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Infrastructure.VehicleLookup;
using SmartParking.Contracts.Identity.Vehicle.V1;
using Xunit;
using IdentityVehicleGrpcService = IdentityWebApi::ParkingSystem.Identity.WebAPI.Grpc.IdentityVehicleGrpcService;
using ProtoVehicleType = SmartParking.Contracts.Identity.Vehicle.V1.VehicleType;

namespace ParkingSystem.Tests.Identity.IntegrationTests.Grpc;

// In-memory gRPC over TestServer: no Docker, no physical port. The client is generated
// from the shared proto like Parking's, so these tests also guard the wire contract.
public sealed class IdentityVehicleGrpcTests
{
    [Fact]
    public async Task Registered_car_returns_canonical_ids_and_car_type()
    {
        await using var host = await GrpcHost.StartAsync();

        var response = await host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = " 51a-123.45 " },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsRegistered);
        Assert.Equal("10000000-0000-0000-0000-000000000001", response.AccountId);
        Assert.Equal("20000000-0000-0000-0000-000000000001", response.VehicleId);
        AssertCanonicalUuid(response.AccountId);
        AssertCanonicalUuid(response.VehicleId);
        Assert.Equal(ProtoVehicleType.Car, response.VehicleType);
        Assert.True(response.IsAccountActive);
    }

    [Fact]
    public async Task Registered_motorbike_returns_motorbike_type()
    {
        await using var host = await GrpcHost.StartAsync();

        var response = await host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "59-X1 123.45" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsRegistered);
        Assert.Equal(ProtoVehicleType.Motorbike, response.VehicleType);
        AssertCanonicalUuid(response.VehicleId);
    }

    [Fact]
    public async Task Unknown_well_formed_plate_returns_ok_and_not_registered()
    {
        await using var host = await GrpcHost.StartAsync();

        var response = await host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "30F-999.99" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.IsRegistered);
        Assert.Equal(string.Empty, response.AccountId);
        Assert.Equal(string.Empty, response.VehicleId);
        Assert.Equal(ProtoVehicleType.Unspecified, response.VehicleType);
        Assert.False(response.IsAccountActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("12")]
    [InlineData("5112345")]
    public async Task Empty_or_malformed_plate_returns_invalid_argument(string plate)
    {
        await using var host = await GrpcHost.StartAsync();

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = plate },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task Unexpected_reader_failure_returns_internal_without_leaking_details()
    {
        await using var host = await GrpcHost.StartAsync(services =>
            services.AddSingleton<IVehicleLookupReader>(new ThrowingReader()));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "51A12345" },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.Internal, exception.StatusCode);
        Assert.DoesNotContain(ThrowingReader.SecretDetail, exception.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_cancellation_reaches_the_reader()
    {
        var reader = new BlockingReader();
        await using var host = await GrpcHost.StartAsync(services =>
            services.AddSingleton<IVehicleLookupReader>(reader));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var call = host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "51A12345" },
            cancellationToken: cancellation.Token);
        await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
        Assert.Equal(StatusCode.Cancelled, exception.StatusCode);
        Assert.True(await reader.ObservedCancellation.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Owner_name_is_not_part_of_the_contract()
    {
        // Field 4 (owner_full_name) is reserved: Parking only receives ids, personal data stays in Identity.
        var descriptor = LookupVehicleResponse.Descriptor;

        Assert.Null(descriptor.FindFieldByNumber(4));
        Assert.Null(descriptor.FindFieldByName("owner_full_name"));
        Assert.Equal([1, 2, 3, 5, 6], descriptor.Fields.InFieldNumberOrder().Select(field => field.FieldNumber));
    }

    [Fact]
    public async Task Registered_vehicle_of_an_inactive_account_stays_registered()
    {
        await using var host = await GrpcHost.StartAsync(services => services.AddSingleton<IVehicleLookupReader>(
            new FixedReader(new VehicleLookupResult(true, Guid.NewGuid(), Guid.NewGuid(), ParkingSystem.Identity.Application.Vehicles.Lookup.VehicleType.Car, IsAccountActive: false))));

        var response = await host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "51A12345" },
            cancellationToken: TestContext.Current.CancellationToken);

        // is_account_active=false must never be read as "not registered".
        Assert.True(response.IsRegistered);
        Assert.False(response.IsAccountActive);
        AssertCanonicalUuid(response.AccountId);
    }

    [Fact]
    public async Task Registered_result_without_identifiers_returns_internal_instead_of_a_half_filled_answer()
    {
        await using var host = await GrpcHost.StartAsync(services => services.AddSingleton<IVehicleLookupReader>(
            new FixedReader(new VehicleLookupResult(true, null, null, null, IsAccountActive: true))));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.LookupVehicleAsync(
            new LookupVehicleRequest { LicensePlate = "51A12345" },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.Internal, exception.StatusCode);
    }

    private static void AssertCanonicalUuid(string value)
    {
        Assert.Equal(36, value.Length);
        Assert.Equal(value.ToLowerInvariant(), value);
        Assert.True(Guid.TryParseExact(value, "D", out _));
    }

    private sealed class GrpcHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        private GrpcHost(WebApplication app, GrpcChannel channel)
        {
            _app = app;
            _channel = channel;
            Client = new IdentityVehicleContract.IdentityVehicleContractClient(channel);
        }

        public IdentityVehicleContract.IdentityVehicleContractClient Client { get; }

        public static async Task<GrpcHost> StartAsync(Action<IServiceCollection>? overrideServices = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddApplicationServices();
            builder.Services.AddVehicleLookupReader(builder.Configuration);
            overrideServices?.Invoke(builder.Services);

            var app = builder.Build();
            app.MapGrpcService<IdentityVehicleGrpcService>();
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

    private sealed class FixedReader(VehicleLookupResult result) : IVehicleLookupReader
    {
        public Task<VehicleLookupResult> FindByPlateAsync(string normalizedLicensePlate, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingReader : IVehicleLookupReader
    {
        public const string SecretDetail = "Host=db;Password=secret";

        public Task<VehicleLookupResult> FindByPlateAsync(string normalizedLicensePlate, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(SecretDetail);
    }

    private sealed class BlockingReader : IVehicleLookupReader
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ObservedCancellation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<VehicleLookupResult> FindByPlateAsync(string normalizedLicensePlate, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation.TrySetResult(true);
                throw;
            }

            throw new InvalidOperationException("Unreachable.");
        }
    }
}
