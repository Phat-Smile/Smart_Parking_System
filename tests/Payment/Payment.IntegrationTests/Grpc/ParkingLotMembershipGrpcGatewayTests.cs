using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.WebAPI.Grpc;
using SmartParking.Contracts.Parking.Membership.V1;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests.Grpc;

public sealed class ParkingLotMembershipGrpcGatewayTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(true, "Staff")]
    [InlineData(true, "Owner")]
    [InlineData(false, "")]
    public async Task Maps_response_fields_from_contract(bool isMember, string role)
    {
        var gateway = CreateGateway(_ => Task.FromResult(new GetLotMembershipResponse
        {
            IsMember = isMember,
            Role = role
        }));

        var result = await gateway.GetMembershipAsync(LotId, AccountId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(isMember, result.Value!.IsMember);
        Assert.Equal(role, result.Value.Role);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "40000000-0000-0000-0000-000000000001")]
    [InlineData("10000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000000")]
    public async Task Empty_ids_fail_closed_without_wire_call(string lotIdStr, string accountIdStr)
    {
        var called = false;
        var gateway = CreateGateway(_ =>
        {
            called = true;
            return Task.FromResult(new GetLotMembershipResponse { IsMember = true, Role = "Staff" });
        });

        var result = await gateway.GetMembershipAsync(
            Guid.Parse(lotIdStr), Guid.Parse(accountIdStr), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable, result.Error);
        Assert.False(called);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.InvalidArgument)]
    public async Task Rpc_failures_fail_closed_as_parking_service_unavailable(StatusCode code)
    {
        var gateway = CreateGateway(_ => Task.FromException<GetLotMembershipResponse>(
            new RpcException(new Status(code, "gRPC connection error"))));

        var result = await gateway.GetMembershipAsync(LotId, AccountId, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable, result.Error);
    }

    [Fact]
    public async Task Sends_service_key_to_parking()
    {
        GetLotMembershipRequest? seen = null;
        Metadata? headers = null;
        var gateway = CreateGateway(
            (request, options) =>
            {
                seen = request;
                headers = options.Headers;
                return Task.FromResult(new GetLotMembershipResponse { IsMember = true, Role = "Staff" });
            },
            serviceKey: "test-membership-service-key");

        await gateway.GetMembershipAsync(LotId, AccountId, TestContext.Current.CancellationToken);

        Assert.Equal(LotId.ToString("D"), seen!.LotId);
        Assert.Equal(AccountId.ToString("D"), seen.AccountId);
        Assert.Equal("test-membership-service-key", headers!.GetValue(ServiceKeyValidationMiddleware.HeaderName));
    }

    [Fact]
    public void Registration_registers_membership_gateway()
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
        var gateway = scope.ServiceProvider.GetRequiredService<IParkingLotMembershipGateway>();
        Assert.IsType<ParkingLotMembershipGrpcGateway>(gateway);
    }

    private static ParkingLotMembershipGrpcGateway CreateGateway(
        Func<GetLotMembershipRequest, Task<GetLotMembershipResponse>> handler) =>
        CreateGateway((request, _) => handler(request));

    private static ParkingLotMembershipGrpcGateway CreateGateway(
        Func<GetLotMembershipRequest, CallOptions, Task<GetLotMembershipResponse>> handler,
        string serviceKey = "") =>
        new(
            new FakeClient(handler),
            Options.Create(new ParkingServiceOptions { GrpcEndpoint = "http://parking-api:8081", TimeoutSeconds = 3 }),
            Options.Create(new ServiceKeyOptions { ServiceKey = serviceKey }),
            NullLogger<ParkingLotMembershipGrpcGateway>.Instance);

    private sealed class FakeClient(
        Func<GetLotMembershipRequest, CallOptions, Task<GetLotMembershipResponse>> handler)
        : ParkingLotMembershipContract.ParkingLotMembershipContractClient
    {
        public override AsyncUnaryCall<GetLotMembershipResponse> GetLotMembershipAsync(
            GetLotMembershipRequest request, CallOptions options) =>
            new(handler(request, options),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
    }
}
