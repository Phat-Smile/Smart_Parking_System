using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class DebitReservationDepositCommandHandlerTests
{
    private class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();
        public bool ThrowConcurrencyOnSave { get; set; }

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
            if (ThrowConcurrencyOnSave)
            {
                throw new Exception("DbUpdateConcurrencyException");
            }
            return Task.CompletedTask;
        }
    }

    private class FakeIdempotencyStore : IIdempotencyStore
    {
        public readonly Dictionary<string, PaymentIdempotencyRecord> Records = new();

        public Task<PaymentIdempotencyRecord?> FindAsync(
            Guid accountId, string operation, string key, CancellationToken ct)
        {
            Records.TryGetValue($"{accountId}:{operation}:{key}", out var record);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var key = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            if (Records.ContainsKey(key))
            {
                return Task.FromResult(false);
            }

            Records[key] = record;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var key = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            Records[key] = record;
            return Task.CompletedTask;
        }

        public Task ReleasePendingAsync(Guid id, CancellationToken ct)
        {
            var item = Records.FirstOrDefault(r => r.Value.Id == id);
            if (!item.Equals(default(KeyValuePair<string, PaymentIdempotencyRecord>)))
            {
                Records.Remove(item.Key);
            }
            return Task.CompletedTask;
        }
    }

    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeIdempotencyStore _idempotencyStore = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
    private readonly NullLogger<DebitReservationDepositCommandHandler> _logger = NullLogger<DebitReservationDepositCommandHandler>.Instance;

    private DebitReservationDepositCommandHandler CreateHandler() =>
        new(_walletRepository, _idempotencyStore, _clock, _logger);

    private Wallet CreateWalletWithBalance(Guid userId, decimal initialBalance)
    {
        var wallet = Wallet.Create(userId);
        if (initialBalance > 0m)
        {
            wallet.AddEntry(LedgerEntryType.TopUp, initialBalance, "Initial topup");
        }
        _walletRepository.SeedWallet(wallet);
        return wallet;
    }

    [Fact]
    public async Task Debit_Succeeds_WhenWalletHasSufficientBalance_ReducesBalance_AndCreatesLedgerEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 100_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 60_000m, "key-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.NotEmpty(result.Value.TransactionId!);
        Assert.Equal(40_000L, result.Value.RemainingBalance);
        Assert.Equal(40_000m, wallet.Balance);

        var lastEntry = wallet.LedgerEntries.Last();
        Assert.Equal(LedgerEntryType.ReservationDeposit, lastEntry.EntryType);
        Assert.Equal(-60_000m, lastEntry.Amount);
        Assert.Equal(40_000m, lastEntry.BalanceAfter);
        Assert.Equal(ReferenceType.Reservation, lastEntry.ReferenceType);
        Assert.Equal(reservationId.ToString(), lastEntry.ReferenceId);
    }

    [Fact]
    public async Task Debit_Fails_WhenWalletNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 60_000m, "key-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.WalletNotFound, result.Error);
    }

    [Fact]
    public async Task Debit_Fails_WhenBalanceIsInsufficient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 20_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 60_000m, "key-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.InsufficientBalance, result.Error);
        Assert.Equal(20_000m, wallet.Balance);
    }

    [Fact]
    public async Task Debit_Idempotency_ReplaysCachedResult_WhenCalledWithSameKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 100_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 60_000m, "same-key");

        // Act 1
        var firstResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(40_000m, wallet.Balance);

        // Act 2
        var secondResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Value!.TransactionId, secondResult.Value!.TransactionId);
        Assert.Equal(40_000L, secondResult.Value!.RemainingBalance);
        Assert.Equal(40_000m, wallet.Balance); // Balance was not deducted twice
    }

    [Fact]
    public async Task Debit_Idempotency_Fails_WhenReusedWithDifferentRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        CreateWalletWithBalance(userId, 100_000m);
        var handler = CreateHandler();
        var command1 = new DebitReservationDepositCommand(userId, reservationId, 60_000m, "reuse-key");
        var command2 = new DebitReservationDepositCommand(userId, reservationId, 50_000m, "reuse-key");

        // Act
        var firstResult = await handler.Handle(command1, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await handler.Handle(command2, CancellationToken.None);

        // Assert
        Assert.True(secondResult.IsFailure);
        Assert.Equal(ReservationDepositErrors.IdempotencyKeyReused, secondResult.Error);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "d88b4eb9-4089-4da6-b328-98e94fa0075e", 1000)]
    [InlineData("d88b4eb9-4089-4da6-b328-98e94fa0075e", "00000000-0000-0000-0000-000000000000", 1000)]
    [InlineData("d88b4eb9-4089-4da6-b328-98e94fa0075e", "d88b4eb9-4089-4da6-b328-98e94fa0075e", 0)]
    [InlineData("d88b4eb9-4089-4da6-b328-98e94fa0075e", "d88b4eb9-4089-4da6-b328-98e94fa0075e", -500)]
    public async Task Debit_Fails_WhenInputsAreInvalid(string userIdStr, string resIdStr, decimal amount)
    {
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(Guid.Parse(userIdStr), Guid.Parse(resIdStr), amount, "key");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
