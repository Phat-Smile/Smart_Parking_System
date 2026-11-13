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
            return Task.CompletedTask;
        }
    }

    private class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly List<PaymentIdempotencyRecord> _records = new();

        public Task<PaymentIdempotencyRecord?> FindAsync(Guid accountId, string operation, string key, CancellationToken cancellationToken)
        {
            var record = _records.FirstOrDefault(r => r.AccountId == accountId && r.Operation == operation && r.IdempotencyKey == key);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken cancellationToken)
        {
            var exists = _records.Any(r => r.AccountId == record.AccountId && r.Operation == record.Operation && r.IdempotencyKey == record.IdempotencyKey);
            if (exists)
            {
                return Task.FromResult(false);
            }

            _records.Add(record);
            return Task.FromResult(true);
        }

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken cancellationToken)
        {
            var existing = _records.FirstOrDefault(r => r.Id == record.Id);
            if (existing != null)
            {
                _records.Remove(existing);
                _records.Add(record);
            }

            return Task.CompletedTask;
        }

        public Task ReleasePendingAsync(Guid id, CancellationToken cancellationToken)
        {
            var toRemove = _records.FirstOrDefault(r => r.Id == id);
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
        // Đã từng bị trừ cọc 50.000đ cho reservationId này
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -50_000m,
            "Deposit",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        _walletRepository.SeedWallet(wallet);

        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "refund-key-1", "Test cancellation refund");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.TransactionId);
        Assert.Equal(100_000, result.Value.RemainingBalance);
        Assert.Equal(100_000m, wallet.Balance);
    }

    [Fact]
    public async Task Handle_Fails_WhenReservationNeverDebited()
    {
        // Arrange (P3 test)
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Initial topup");
        _walletRepository.SeedWallet(wallet);

        // Gọi hoàn 50.000đ khi chưa từng bị trừ cọc
        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "refund-never-debited");
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ReservationNotDebited, result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenRefundAmountExceedsDebitedAmount()
    {
        // Arrange (P3 test)
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Topup");
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -20_000m,
            "Deposit 20k",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        _walletRepository.SeedWallet(wallet);

        // Đã cọc 20k nhưng đòi hoàn 50k
        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "refund-exceeds");
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.RefundAmountExceedsDeposit, result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenAlreadyFullyRefunded()
    {
        // Arrange
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Topup");
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -20_000m,
            "Deposit 20k",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        wallet.AddEntry(
            LedgerEntryType.Refund,
            20_000m,
            "Refund 20k",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        _walletRepository.SeedWallet(wallet);

        var command = new RefundReservationDepositCommand(userId, reservationId, 20_000m, "refund-already-done");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.AlreadyRefunded, result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenWalletNotFound_DoesNotCreatePhantomWallet()
    {
        // Arrange (P3 test: Cấm tự tạo ví mới)
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "refund-no-wallet");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.WalletNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenReservationDebitedByAnotherUser()
    {
        // Arrange (Lỗ hổng 3 Anti-IDOR test)
        var handler = CreateHandler();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        // User A cọc tiền
        var walletA = Wallet.Create(userA);
        walletA.AddEntry(LedgerEntryType.TopUp, 100_000m, "Topup A");
        walletA.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -20_000m,
            "Deposit by A",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userA);
        _walletRepository.SeedWallet(walletA);

        // User B có ví riêng
        var walletB = Wallet.Create(userB);
        walletB.AddEntry(LedgerEntryType.TopUp, 10_000m, "Topup B");
        _walletRepository.SeedWallet(walletB);

        // User B yêu cầu hoàn tiền cho đặt chỗ của User A
        var command = new RefundReservationDepositCommand(userB, reservationId, 20_000m, "refund-b-steals-a");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ReservationNotDebited, result.Error);
    }

    [Fact]
    public async Task Handle_IdempotentReplay_ReturnsCachedResult()
    {
        // Arrange (M2 test)
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Initial");
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -50_000m,
            "Deposit",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        _walletRepository.SeedWallet(wallet);

        var command = new RefundReservationDepositCommand(userId, reservationId, 50_000m, "same-refund-key");

        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal(100_000, result1.Value!.RemainingBalance);

        // Replay with identical key & payload (M2: check Replay success đầu tiên, không bị lỗi vượt hạn mức)
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value.TransactionId, result2.Value!.TransactionId);
        Assert.Equal(100_000m, wallet.Balance); // Số dư không bị cộng lần 2
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
}
