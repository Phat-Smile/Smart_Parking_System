using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<IReadOnlyList<WalletLedgerEntry>> GetReservationLedgerEntriesAsync(
            Guid reservationId,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            var resIdStr = reservationId.ToString();
            var entries = _wallets.Values
                .Where(w => !userId.HasValue || w.UserId == userId.Value)
                .SelectMany(w => w.LedgerEntries)
                .Where(e => e.ReferenceType == ReferenceType.Reservation && e.ReferenceId == resIdStr)
                .ToList();

            return Task.FromResult<IReadOnlyList<WalletLedgerEntry>>(entries);
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
        if (initialBalance > 0)
        {
            wallet.AddEntry(LedgerEntryType.TopUp, initialBalance, "Initial balance for test");
        }
        _walletRepository.SeedWallet(wallet);
        return wallet;
    }

    [Fact]
    public async Task Debit_Success_DeductsBalance_AndWritesLedger()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 50_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, "key-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.TransactionId);
        Assert.Equal(30_000L, result.Value!.RemainingBalance);
        Assert.Equal(30_000m, wallet.Balance);
        Assert.Contains(wallet.LedgerEntries, e => e.EntryType == LedgerEntryType.ReservationDeposit && e.Amount == -20_000m);
    }

    [Fact]
    public async Task Debit_Fails_WhenWalletNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, "key-not-found");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.WalletNotFound, result.Error);
    }

    [Fact]
    public async Task Debit_Fails_WhenBalanceIsInsufficient_AndReleasesLockForRetry()
    {
        // Arrange (P4 test)
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 10_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, "insufficient-key");

        // Act 1: Thất bại vì thiếu tiền
        var result1 = await handler.Handle(command, CancellationToken.None);

        // Assert 1: Thất bại và đã giải phóng lock
        Assert.True(result1.IsFailure);
        Assert.Equal(ReservationDepositErrors.InsufficientBalance, result1.Error);
        Assert.Empty(_idempotencyStore.Records); // Đã gọi ReleasePendingAsync

        // Act 2: Khách nạp thêm 50.000đ và thử lại với cùng Idempotency Key
        wallet.AddEntry(LedgerEntryType.TopUp, 50_000m, "Topup more funds");
        var result2 = await handler.Handle(command, CancellationToken.None);

        // Assert 2: Thành công thông luồng
        Assert.True(result2.IsSuccess);
        Assert.Equal(40_000L, result2.Value!.RemainingBalance);
        Assert.Equal(40_000m, wallet.Balance);
    }

    [Fact]
    public async Task Debit_Fails_WhenAlreadyRefunded_EvenIfCachedSuccessExists()
    {
        // Arrange (M1 & P5 test)
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 50_000m);
        wallet.AddEntry(
            LedgerEntryType.Refund,
            20_000m,
            "Refunded already",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);

        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, "key-refunded");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Chốt chặn M1 từ chối ngay trước khi Replay
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.AlreadyRefunded, result.Error);
    }

    [Fact]
    public async Task Debit_PurposeScheduled_SetsCorrectLedgerReason()
    {
        // Arrange (P7 test)
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 50_000m);
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(
            userId, reservationId, 20_000m, "key-scheduled", ReservationPrepaymentPurpose.Scheduled);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var entry = wallet.LedgerEntries.First(e => e.EntryType == LedgerEntryType.ReservationDeposit);
        Assert.Contains("Scheduled reservation prepayment", entry.Reason);
    }

    [Fact]
    public async Task Debit_Idempotent_Replay_ReturnsSameResult_WithoutDoubleDeducting()
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
        Assert.Equal(40_000m, wallet.Balance);
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
    [InlineData("d88b4eb9-4089-4da6-b328-98e94fa0075e", "d88b4eb9-4089-4da6-b328-98e94fa0075e", 1000.5)]
    public async Task Debit_Fails_WhenInputsAreInvalid(string userIdStr, string resIdStr, decimal amount)
    {
        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(Guid.Parse(userIdStr), Guid.Parse(resIdStr), amount, "key");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Debit_FailsWithAlreadyForfeited_WhenReservationWasForfeited()
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = CreateWalletWithBalance(userId, 100_000m);
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -20_000m,
            "Deposit",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        wallet.AddEntry(
            LedgerEntryType.PrepaymentForfeited,
            20_000m,
            "Forfeited",
            ReferenceType.Reservation,
            reservationId.ToString());

        var handler = CreateHandler();
        var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, "key-after-forfeit");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.AlreadyForfeited, result.Error);
        Assert.Equal(80_000m, wallet.Balance);
    }
}
