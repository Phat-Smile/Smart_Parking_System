extern alias ParkingWebApi;

using Grpc.Core;
using Grpc.Net.Client;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots.GetLotMembership;
using SmartParking.Contracts.Parking.Membership.V1;
using Xunit;
using ParkingLotMembershipGrpcService = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.ParkingLotMembershipGrpcService;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingLotMembershipGrpcTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    [Theory]
    [InlineData("Staff")]
    [InlineData("Owner")]
    public async Task Member_returns_is_member_true_and_assigned_role(string role)
    {
        var dto = new LotMembershipDto(true, role);
        await using var host = await GrpcHost.StartAsync(Result<LotMembershipDto>.Success(dto));

        var response = await host.Client.GetLotMembershipAsync(
            new GetLotMembershipRequest
            {
                LotId = LotId.ToString("D"),
                AccountId = AccountId.ToString("D")
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.IsMember);
        Assert.Equal(role, response.Role);
    }

    [Fact]
    public async Task Non_member_returns_is_member_false_and_empty_role_without_error()
    {
        var dto = new LotMembershipDto(false, string.Empty);
        await using var host = await GrpcHost.StartAsync(Result<LotMembershipDto>.Success(dto));

        var response = await host.Client.GetLotMembershipAsync(
            new GetLotMembershipRequest
            {
                LotId = LotId.ToString("D"),
                AccountId = AccountId.ToString("D")
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.IsMember);
        Assert.Equal(string.Empty, response.Role);
    }

    [Theory]
    [InlineData("", "30000000-0000-0000-0000-000000000003")]
    [InlineData("not-a-uuid", "30000000-0000-0000-0000-000000000003")]
    [InlineData("10000000-0000-0000-0000-000000000001", "")]
    [InlineData("10000000-0000-0000-0000-000000000001", "bad-uuid")]
    public async Task Malformed_ids_return_invalid_argument(string lotIdStr, string accountIdStr)
    {
        await using var host = await GrpcHost.StartAsync(Result<LotMembershipDto>.Success(new LotMembershipDto(false, "")));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetLotMembershipAsync(
            new GetLotMembershipRequest
            {
                LotId = lotIdStr,
                AccountId = accountIdStr
            },
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
            Client = new ParkingLotMembershipContract.ParkingLotMembershipContractClient(channel);
        }

        public ParkingLotMembershipContract.ParkingLotMembershipContractClient Client { get; }

        public static async Task<GrpcHost> StartAsync(Result<LotMembershipDto> result)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<ISender>(new FixedSender(result));

            var app = builder.Build();
            app.MapGrpcService<ParkingLotMembershipGrpcService>();
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

    private sealed class FixedSender(Result<LotMembershipDto> result) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)(object)result);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
