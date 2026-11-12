using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class RefundReservationDepositCommandHandlerTests
{
    private class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();

        public void SeedWallet(Wallet wallet)
        {
            _wallets[wallet.UserId] = wallet;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var wallet = _wallets.Values.FirstOrDefault(w => w.Id == id);
            return Task.FromResult(wallet);
        }

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            _wallets.TryGetValue(userId, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (_wallets.TryGetValue(userId, out var wallet))
            {
                return Task.FromResult(wallet);
            }

            var newWallet = Wallet.Create(userId);
            _wallets[userId] = newWallet;
            return Task.FromResult(newWallet);
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
        {
            _wallets[wallet.UserId] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, PaymentIdempotencyRecord> _records = new();
        public bool ReleaseCalled { get; private set; }

        public Task<PaymentIdempotencyRecord?> FindAsync(Guid userId, string operation, string key, CancellationToken ct)
        {
            var compoundKey = $"{userId}:{operation}:{key}";
            _records.TryGetValue(compoundKey, out var record);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var compoundKey = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            if (_records.ContainsKey(compoundKey))
            {
                return Task.FromResult(false);
            }

            _records[compoundKey] = record;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var compoundKey = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            _records[compoundKey] = record;
            return Task.CompletedTask;
        }

        public Task ReleasePendingAsync(Guid recordId, CancellationToken ct)
        {
            ReleaseCalled = true;
            var toRemove = _records.FirstOrDefault(r => r.Value.Id == recordId).Key;
            if (toRemove != null)
            {
                _records.Remove(toRemove);
            }

            return Task.CompletedTask;
        }
    }

    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeIdempotencyStore _idempotencyStore = new();
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);
    private readonly NullLogger<RefundReservationDepositCommandHandler> _logger = NullLogger<RefundReservationDepositCommandHandler>.Instance;

    private RefundReservationDepositCommandHandler CreateHandler() =>
        new(_walletRepository, _idempotencyStore, _clock, _logger);

    [Fact]
    public async Task Handle_ValidRefund_CreditsWalletAndReturnsSuccess()
    {
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Initial topup");
        _walletRepository.SeedWallet(wallet);

        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "refund-key-1", "Test cancellation refund");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.TransactionId);
        Assert.Equal(150_000, result.Value.RemainingBalance);
        Assert.Equal(150_000m, wallet.Balance);
    }

    [Fact]
    public async Task Handle_InvalidUserId_ReturnsFailure()
    {
        var handler = CreateHandler();
        var command = new RefundReservationDepositCommand(Guid.Empty, Guid.NewGuid(), 50_000m, "key");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationDepositErrors.UserIdInvalid.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_InvalidReservationId_ReturnsFailure()
    {
        var handler = CreateHandler();
        var command = new RefundReservationDepositCommand(Guid.NewGuid(), Guid.Empty, 50_000m, "key");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationDepositErrors.ReservationIdInvalid.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_InvalidAmount_ReturnsFailure()
    {
        var handler = CreateHandler();
        var command = new RefundReservationDepositCommand(Guid.NewGuid(), Guid.NewGuid(), 0m, "key");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationDepositErrors.AmountInvalid.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_IdempotentReplay_ReturnsCachedResult()
    {
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Initial");
        _walletRepository.SeedWallet(wallet);

        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "same-refund-key");

        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal(150_000, result1.Value!.RemainingBalance);

        // Replay with identical key & payload
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value.TransactionId, result2.Value!.TransactionId);
        Assert.Equal(150_000m, wallet.Balance); // Wallet not credited twice!
    }
}
