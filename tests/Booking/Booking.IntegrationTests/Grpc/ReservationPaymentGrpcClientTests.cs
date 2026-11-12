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
using ParkingSystem.Booking.Application.Payments;
using SmartParking.Contracts.Payment.Reservation.V1;
using Xunit;

using BookingGrpcClient = BookingWebApi::SmartParking.Contracts.Payment.Reservation.V1.ReservationWalletContract.ReservationWalletContractClient;
using BookingReservationPaymentGrpcClient = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.ReservationPaymentGrpcClient;
using BookingPaymentServiceOptions = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.PaymentServiceOptions;
using DomainPrepaymentPurpose = ParkingSystem.Booking.Application.Payments.PrepaymentPurpose;
using ProtoPrepaymentPurpose = SmartParking.Contracts.Payment.Reservation.V1.PrepaymentPurpose;

namespace ParkingSystem.Tests.Booking.IntegrationTests.Grpc;

public sealed class ReservationPaymentGrpcClientTests
{
    private static readonly Guid TestReservationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestAccountId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TestLotId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task T_17a_Successful_debit_returns_success_result()
    {
        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (req, ctx) => Task.FromResult(new DebitReservationResponse
            {
                Outcome = DebitOutcome.Succeeded,
                LedgerEntryId = "LEDGER-999",
                BalanceAfterVnd = 50000
            }));
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationDebitOutcome.Succeeded, result.Value!.Outcome);
        Assert.Equal("LEDGER-999", result.Value.LedgerEntryId);
        Assert.Equal(50000, result.Value.BalanceAfterVnd);
    }

    [Fact]
    public async Task T_17b_Status_mapping_unavailable()
    {
        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (_, _) => throw new RpcException(new Status(StatusCode.Unavailable, "Service Unavailable")));
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationPaymentErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task T_17c_Status_mapping_deadline_exceeded()
    {
        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (_, _) => throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline Exceeded")));
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationPaymentErrors.DeadlineExceeded, result.Error);
    }

    [Fact]
    public async Task T_17d_Status_mapping_service_auth_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (_, _) => throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid service key")));
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationPaymentErrors.ServiceAuthFailed, result.Error);
    }

    [Fact]
    public async Task T_17e_Status_mapping_communication_failed()
    {
        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (_, _) => throw new RpcException(new Status(StatusCode.Internal, "Internal Server Error")));
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationPaymentErrors.CommunicationFailed, result.Error);
    }

    [Fact]
    public async Task T_17f_Deadline_and_x_service_key_header_are_sent()
    {
        DateTime? serverReceivedDeadline = null;
        string? serverReceivedKey = null;

        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (req, ctx) =>
            {
                serverReceivedDeadline = ctx.Deadline;
                serverReceivedKey = ctx.RequestHeaders.GetValue(ServiceKeyValidationMiddleware.HeaderName.ToLowerInvariant())
                    ?? ctx.RequestHeaders.GetValue(ServiceKeyValidationMiddleware.HeaderName);
                return Task.FromResult(new DebitReservationResponse { Outcome = DebitOutcome.Succeeded });
            });
        var client = host.CreateClient(timeoutSeconds: 8, clientServiceKey: "secret-key-12345");

        var beforeCall = DateTime.UtcNow;
        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("secret-key-12345", serverReceivedKey);
        Assert.NotNull(serverReceivedDeadline);
        Assert.True(serverReceivedDeadline.Value >= beforeCall.AddSeconds(7));
        Assert.True(serverReceivedDeadline.Value <= beforeCall.AddSeconds(9));
    }

    [Fact]
    public async Task T_17g_Cancellation_token_propagation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (req, ctx) => Task.FromResult(new DebitReservationResponse { Outcome = DebitOutcome.Succeeded }));
        var client = host.CreateClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 20000, DomainPrepaymentPurpose.Deposit, cts.Token));
    }

    [Theory]
    [InlineData(DomainPrepaymentPurpose.Deposit, ProtoPrepaymentPurpose.Deposit)]
    [InlineData(DomainPrepaymentPurpose.Scheduled, ProtoPrepaymentPurpose.Scheduled)]
    public async Task T_17h_Purpose_mapping_verifies_proto_request(DomainPrepaymentPurpose domainPurpose, ProtoPrepaymentPurpose expectedProtoPurpose)
    {
        ProtoPrepaymentPurpose? receivedPurpose = null;

        await using var host = await FakeGrpcServer.StartAsync(
            debitHandler: (req, ctx) =>
            {
                receivedPurpose = req.Purpose;
                return Task.FromResult(new DebitReservationResponse { Outcome = DebitOutcome.Succeeded });
            });
        var client = host.CreateClient();

        var result = await client.DebitAsync(TestReservationId, TestAccountId, TestLotId, 50000, domainPurpose, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedProtoPurpose, receivedPurpose);
    }

    // =========================================================================
    // In-memory TestServer Double
    // =========================================================================
    private sealed class FakeGrpcServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        private FakeGrpcServer(WebApplication app, GrpcChannel channel)
        {
            _app = app;
            _channel = channel;
        }

        public IReservationPaymentClient CreateClient(int timeoutSeconds = 5, string? clientServiceKey = null)
        {
            var grpcClient = new BookingGrpcClient(_channel);
            var options = Options.Create(new BookingPaymentServiceOptions { TimeoutSeconds = timeoutSeconds });
            var serviceKeyOpts = Options.Create(new ServiceKeyOptions { ServiceKey = clientServiceKey ?? string.Empty });
            var logger = LoggerFactory.Create(builder => { }).CreateLogger<BookingReservationPaymentGrpcClient>();
            return new BookingReservationPaymentGrpcClient(grpcClient, options, serviceKeyOpts, logger);
        }

        public static async Task<FakeGrpcServer> StartAsync(
            Func<DebitReservationRequest, ServerCallContext, Task<DebitReservationResponse>>? debitHandler = null,
            Func<GetReservationDebitRequest, ServerCallContext, Task<GetReservationDebitResponse>>? getDebitHandler = null,
            Func<RefundReservationRequest, ServerCallContext, Task<RefundReservationResponse>>? refundHandler = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton(new FakePaymentService(debitHandler, getDebitHandler, refundHandler));

            var app = builder.Build();
            app.MapGrpcService<FakePaymentService>();
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

    private sealed class FakePaymentService(
        Func<DebitReservationRequest, ServerCallContext, Task<DebitReservationResponse>>? debitHandler,
        Func<GetReservationDebitRequest, ServerCallContext, Task<GetReservationDebitResponse>>? getDebitHandler,
        Func<RefundReservationRequest, ServerCallContext, Task<RefundReservationResponse>>? refundHandler)
        : ReservationWalletContract.ReservationWalletContractBase
    {
        public override Task<DebitReservationResponse> DebitReservation(DebitReservationRequest request, ServerCallContext context) =>
            debitHandler != null ? debitHandler(request, context) : base.DebitReservation(request, context);

        public override Task<GetReservationDebitResponse> GetReservationDebit(GetReservationDebitRequest request, ServerCallContext context) =>
            getDebitHandler != null ? getDebitHandler(request, context) : base.GetReservationDebit(request, context);

        public override Task<RefundReservationResponse> RefundReservation(RefundReservationRequest request, ServerCallContext context) =>
            refundHandler != null ? refundHandler(request, context) : base.RefundReservation(request, context);
    }
}

