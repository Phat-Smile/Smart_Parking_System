using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.WebAPI.Grpc;
using SmartParking.Contracts.Parking.SessionFee.V1;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests.Grpc;

public sealed class ParkingSessionFeeGrpcGatewayTests
{
    private static readonly Guid SessionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid BookingId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Maps_every_field_of_the_contract_response()
    {
        var gateway = CreateGateway(_ => Task.FromResult(ValidResponse()));

        var result = await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var ctx = result.Value!;
        Assert.Equal(SessionId, ctx.SessionId);
        Assert.Equal(LotId, ctx.ParkingLotId);
        Assert.Equal("51A-123.45", ctx.PlateNumber);
        Assert.Equal("Car", ctx.VehicleType);
        Assert.Equal("Active", ctx.Status);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 3, 0, 0, TimeSpan.Zero), ctx.EntryTimeUtc);
        Assert.Equal(AccountId, ctx.AccountId);
        Assert.Equal(BookingId, ctx.BookingId);
        Assert.Equal("policy-1", ctx.Pricing.PolicyId);
        Assert.Equal("Block", ctx.Pricing.CalculationMethod);
        Assert.Equal(60, ctx.Pricing.BlockMinutes);
        Assert.Equal(20000L, ctx.Pricing.PricePerUnit);
    }

    [Fact]
    public async Task Empty_account_and_booking_ids_become_null()
    {
        var gateway = CreateGateway(_ =>
        {
            var response = ValidResponse();
            response.AccountId = string.Empty;
            response.BookingId = string.Empty;
            return Task.FromResult(response);
        });

        var result = await gateway.GetFeeContextAsync(SessionId, null, "Staff", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AccountId);
        Assert.Null(result.Value.BookingId);
    }

    [Fact]
    public async Task Empty_session_id_is_rejected_without_a_wire_call()
    {
        var called = false;
        var gateway = CreateGateway(_ =>
        {
            called = true;
            return Task.FromResult(ValidResponse());
        });

        var result = await gateway.GetFeeContextAsync(Guid.Empty, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionIdInvalid, result.Error);
        Assert.False(called);
    }

    [Fact]
    public async Task Not_found_maps_to_session_not_found()
    {
        var gateway = CreateGateway(_ => Task.FromException<GetSessionFeeContextResponse>(
            new RpcException(new Status(StatusCode.NotFound, "no session"))));

        var result = await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.Equal(ParkingFeeErrors.SessionNotFound, result.Error);
    }

    [Theory]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.InvalidArgument)]
    public async Task Other_rpc_failures_fail_closed_as_parking_service_unavailable(StatusCode code)
    {
        var gateway = CreateGateway(_ => Task.FromException<GetSessionFeeContextResponse>(
            new RpcException(new Status(code, "boom"))));

        var result = await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable, result.Error);
    }

    [Theory]
    [InlineData("price_zero")]
    [InlineData("no_pricing")]
    [InlineData("block_without_minutes")]
    [InlineData("unknown_method")]
    [InlineData("bad_entry_time")]
    [InlineData("bad_lot_id")]
    [InlineData("other_session_id")]
    [InlineData("missing_plate")]
    public async Task Invalid_response_never_yields_a_price(string defect)
    {
        var gateway = CreateGateway(_ =>
        {
            var r = ValidResponse();
            switch (defect)
            {
                case "price_zero": r.Pricing.PricePerUnit = 0; break;
                case "no_pricing": r.Pricing = null; break;
                case "block_without_minutes": r.Pricing.BlockMinutes = 0; break;
                case "unknown_method": r.Pricing.CalculationMethod = "Flat"; break;
                case "bad_entry_time": r.EntryTimeUtc = "yesterday"; break;
                case "bad_lot_id": r.LotId = "not-a-guid"; break;
                case "other_session_id": r.SessionId = Guid.NewGuid().ToString("D"); break;
                case "missing_plate": r.PlateNumber = ""; break;
            }
            return Task.FromResult(r);
        });

        var result = await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable, result.Error);
    }

    [Fact]
    public async Task Per_entry_pricing_needs_no_block_minutes()
    {
        var gateway = CreateGateway(_ =>
        {
            var r = ValidResponse();
            r.Pricing.CalculationMethod = "PerEntry";
            r.Pricing.BlockMinutes = 0;
            return Task.FromResult(r);
        });

        var result = await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("PerEntry", result.Value!.Pricing.CalculationMethod);
    }

    [Fact]
    public async Task Sends_session_id_and_service_key_to_parking()
    {
        GetSessionFeeContextRequest? seen = null;
        Metadata? headers = null;
        var gateway = CreateGateway(
            (request, options) =>
            {
                seen = request;
                headers = options.Headers;
                return Task.FromResult(ValidResponse());
            },
            serviceKey: "test-service-key");

        await gateway.GetFeeContextAsync(SessionId, AccountId, "Customer", TestContext.Current.CancellationToken);

        Assert.Equal(SessionId.ToString("D"), seen!.SessionId);
        Assert.Equal("test-service-key", headers!.GetValue(ServiceKeyValidationMiddleware.HeaderName));
    }

    [Fact]
    public void Registration_replaces_the_unavailable_stub_with_the_grpc_gateway()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<ServiceKeyOptions>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParkingService:GrpcEndpoint"] = "http://parking-api:8081"
            })
            .Build();

        services.AddParkingSessionFeeClient(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IParkingSessionGateway>();
        Assert.IsType<ParkingSessionFeeGrpcGateway>(gateway);
    }

    [Fact]
    public void Registration_rejects_a_missing_endpoint()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddParkingSessionFeeClient(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ParkingServiceOptions>>().Value);
    }

    private static GetSessionFeeContextResponse ValidResponse() => new()
    {
        SessionId = SessionId.ToString("D"),
        LotId = LotId.ToString("D"),
        PlateNumber = "51A-123.45",
        VehicleType = "Car",
        Status = "Active",
        EntryTimeUtc = "2026-11-06T03:00:00Z",
        CustomerType = "Booking",
        AccountId = AccountId.ToString("D"),
        BookingId = BookingId.ToString("D"),
        Pricing = new SessionPricing
        {
            PolicyId = "policy-1",
            CalculationMethod = "Block",
            BlockMinutes = 60,
            PricePerUnit = 20000
        }
    };

    private static ParkingSessionFeeGrpcGateway CreateGateway(
        Func<GetSessionFeeContextRequest, Task<GetSessionFeeContextResponse>> handler) =>
        CreateGateway((request, _) => handler(request));

    private static ParkingSessionFeeGrpcGateway CreateGateway(
        Func<GetSessionFeeContextRequest, CallOptions, Task<GetSessionFeeContextResponse>> handler,
        string serviceKey = "") =>
        new(
            new FakeClient(handler),
            Options.Create(new ParkingServiceOptions { GrpcEndpoint = "http://parking-api:8081", TimeoutSeconds = 5 }),
            Options.Create(new ServiceKeyOptions { ServiceKey = serviceKey }),
            NullLogger<ParkingSessionFeeGrpcGateway>.Instance);

    private sealed class FakeClient(
        Func<GetSessionFeeContextRequest, CallOptions, Task<GetSessionFeeContextResponse>> handler)
        : ParkingSessionFeeContract.ParkingSessionFeeContractClient
    {
        public override AsyncUnaryCall<GetSessionFeeContextResponse> GetSessionFeeContextAsync(
            GetSessionFeeContextRequest request, CallOptions options) =>
            new(handler(request, options),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
    }
}
