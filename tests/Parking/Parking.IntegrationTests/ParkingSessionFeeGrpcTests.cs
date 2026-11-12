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
using ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;
using SmartParking.Contracts.Parking.SessionFee.V1;
using Xunit;
using ParkingSessionFeeGrpcService = ParkingWebApi::ParkingSystem.Parking.WebAPI.Grpc.ParkingSessionFeeGrpcService;
using SessionPricing = ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext.SessionPricing;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Contract 6 over an in-memory TestServer: no Docker. The client is generated from the shared proto the same way Payment
// will generate it, so these tests guard the wire contract. The query itself is covered by GetSessionFeeContextTests.
public sealed class ParkingSessionFeeGrpcTests
{
    private static readonly Guid SessionId = Guid.Parse("2b684220-8663-48a3-aed6-a1ef40748c3a");
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PolicyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 6, 1, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public async Task A_known_session_returns_what_payment_needs()
    {
        var fee = new SessionFeeContext(
            SessionId, LotId, "51A-123.45", "Car", "Active", EntryTime, "RegisteredWalkIn", AccountId, null,
            new SessionPricing(PolicyId, "Block", 30, 15000));
        await using var host = await GrpcHost.StartAsync(Result<SessionFeeContext>.Success(fee));

        var response = await host.Client.GetSessionFeeContextAsync(
            new GetSessionFeeContextRequest { SessionId = SessionId.ToString("D") },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SessionId.ToString("D"), response.SessionId);
        Assert.Equal(LotId.ToString("D"), response.LotId);
        Assert.Equal("51A-123.45", response.PlateNumber);
        Assert.Equal("Car", response.VehicleType);
        Assert.Equal("Active", response.Status);
        Assert.Equal(EntryTime, DateTimeOffset.Parse(response.EntryTimeUtc));
        Assert.EndsWith("+00:00", response.EntryTimeUtc);
        Assert.Equal("RegisteredWalkIn", response.CustomerType);
        Assert.Equal(AccountId.ToString("D"), response.AccountId);
        Assert.Equal(string.Empty, response.BookingId);
        Assert.Equal(PolicyId.ToString("D"), response.Pricing.PolicyId);
        Assert.Equal("Block", response.Pricing.CalculationMethod);
        Assert.Equal(30, response.Pricing.BlockMinutes);
        Assert.Equal(15000, response.Pricing.PricePerUnit);
    }

    [Fact]
    public async Task Unknown_session_returns_not_found()
    {
        await using var host = await GrpcHost.StartAsync(Result<SessionFeeContext>.Failure(GetSessionFeeContextErrors.SessionNotFound));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetSessionFeeContextAsync(
            new GetSessionFeeContextRequest { SessionId = SessionId.ToString("D") },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task No_price_list_at_entry_returns_failed_precondition()
    {
        await using var host = await GrpcHost.StartAsync(Result<SessionFeeContext>.Failure(GetSessionFeeContextErrors.PricingNotFound));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetSessionFeeContextAsync(
            new GetSessionFeeContextRequest { SessionId = SessionId.ToString("D") },
            cancellationToken: TestContext.Current.CancellationToken).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    public async Task Malformed_session_id_returns_invalid_argument(string sessionId)
    {
        await using var host = await GrpcHost.StartAsync(Result<SessionFeeContext>.Failure(GetSessionFeeContextErrors.SessionNotFound));

        var exception = await Assert.ThrowsAsync<RpcException>(() => host.Client.GetSessionFeeContextAsync(
            new GetSessionFeeContextRequest { SessionId = sessionId },
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
            Client = new ParkingSessionFeeContract.ParkingSessionFeeContractClient(channel);
        }

        public ParkingSessionFeeContract.ParkingSessionFeeContractClient Client { get; }

        public static async Task<GrpcHost> StartAsync(Result<SessionFeeContext> result)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<ISender>(new FixedSender(result));

            var app = builder.Build();
            app.MapGrpcService<ParkingSessionFeeGrpcService>();
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

    private sealed class FixedSender(Result<SessionFeeContext> result) : ISender
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
