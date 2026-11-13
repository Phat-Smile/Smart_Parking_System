using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.WebAPI.Grpc;
using SmartParking.Contracts.Payment.Reservation.V1;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests.Grpc;

public sealed class ReservationWalletGrpcServiceTests
{
    private class DummyServerCallContext : ServerCallContext
    {
        public static readonly DummyServerCallContext Instance = new();
        protected override string MethodCore => "Test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "127.0.0.1";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    private class FakeSender : ISender
    {
        public object? Response { get; set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((TResponse)Response!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Response);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class FakeWalletRepository : IWalletRepository
    {
        public List<WalletLedgerEntry> Entries { get; } = new();

        public Task<IReadOnlyList<WalletLedgerEntry>> GetReservationLedgerEntriesAsync(
            Guid reservationId,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WalletLedgerEntry>>(Entries);
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Wallet?>(null);
        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<Wallet?>(null);
        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private readonly FakeSender _sender = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly NullLogger<ReservationWalletGrpcService> _logger = NullLogger<ReservationWalletGrpcService>.Instance;

    private ReservationWalletGrpcService CreateService() =>
        new(_sender, _walletRepository, _logger);

    [Fact]
    public async Task DebitReservation_Success_MapsToSucceeded()
    {
        var service = CreateService();
        _sender.Response = Result<DebitReservationDepositResult>.Success(new DebitReservationDepositResult(
            Success: true,
            TransactionId: "txn-123",
            ErrorCode: null,
            ErrorMessage: null,
            RemainingBalance: 80_000));

        var request = new DebitReservationRequest
        {
            AccountId = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            AmountVnd = 20_000,
            Purpose = PrepaymentPurpose.Deposit
        };

        var response = await service.DebitReservation(request, DummyServerCallContext.Instance);

        Assert.Equal(DebitOutcome.Succeeded, response.Outcome);
        Assert.Equal("txn-123", response.LedgerEntryId);
        Assert.Equal(80_000, response.BalanceAfterVnd);
    }

    [Theory]
    [InlineData("payment.wallet_insufficient_balance", DebitOutcome.InsufficientBalance)]
    [InlineData("payment.wallet_not_found", DebitOutcome.InsufficientBalance)]
    [InlineData("payment.wallet_not_active", DebitOutcome.WalletNotActive)]
    [InlineData("payment.reservation_already_refunded", DebitOutcome.AlreadyRefunded)]
    [InlineData("payment.idempotency_key_reused", DebitOutcome.AmountMismatch)]
    [InlineData("payment.some_random_error", DebitOutcome.Unspecified)]
    public async Task DebitReservation_ErrorMapping_MatchesContract(string errorCode, DebitOutcome expectedOutcome)
    {
        var service = CreateService();
        _sender.Response = Result<DebitReservationDepositResult>.Failure(new Error(errorCode, "Test error"));

        var request = new DebitReservationRequest
        {
            AccountId = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            AmountVnd = 20_000
        };

        var response = await service.DebitReservation(request, DummyServerCallContext.Instance);

        Assert.Equal(expectedOutcome, response.Outcome);
    }

    [Fact]
    public async Task GetReservationDebit_InvalidAccountId_ReturnsUnspecified_WithoutQuerying()
    {
        var service = CreateService();
        var request = new GetReservationDebitRequest
        {
            AccountId = "invalid-guid",
            ReservationId = Guid.NewGuid().ToString()
        };

        var response = await service.GetReservationDebit(request, DummyServerCallContext.Instance);

        Assert.Equal(DebitState.Unspecified, response.State);
        Assert.Equal(0, response.AmountVnd);
    }

    [Fact]
    public async Task GetReservationDebit_EmptyEntries_ReturnsNotDebited()
    {
        var service = CreateService();
        var request = new GetReservationDebitRequest
        {
            AccountId = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString()
        };

        var response = await service.GetReservationDebit(request, DummyServerCallContext.Instance);

        Assert.Equal(DebitState.NotDebited, response.State);
        Assert.Equal(0, response.AmountVnd);
    }

    [Fact]
    public async Task RefundReservation_Success_ReturnsRefunded()
    {
        var service = CreateService();
        _sender.Response = Result<RefundReservationDepositResult>.Success(new RefundReservationDepositResult(
            Success: true,
            TransactionId: "refund-tx-1",
            ErrorCode: null,
            ErrorMessage: null,
            RemainingBalance: 100_000));

        var request = new RefundReservationRequest
        {
            AccountId = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            AmountVnd = 20_000,
            Reason = "User cancelled"
        };

        var response = await service.RefundReservation(request, DummyServerCallContext.Instance);

        Assert.Equal(DebitState.Refunded, response.State);
        Assert.Equal("refund-tx-1", response.LedgerEntryId);
    }

    [Theory]
    [InlineData("payment.reservation_not_debited", DebitState.NotDebited)]
    [InlineData("payment.reservation_already_refunded", DebitState.Refunded)]
    [InlineData("payment.unknown_error", DebitState.Unspecified)]
    public async Task RefundReservation_Failure_MapsErrorStateProperly(string errorCode, DebitState expectedState)
    {
        var service = CreateService();
        _sender.Response = Result<RefundReservationDepositResult>.Failure(new Error(errorCode, "Test error"));

        var request = new RefundReservationRequest
        {
            AccountId = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            AmountVnd = 20_000
        };

        var response = await service.RefundReservation(request, DummyServerCallContext.Instance);

        Assert.Equal(expectedState, response.State);
    }
}
