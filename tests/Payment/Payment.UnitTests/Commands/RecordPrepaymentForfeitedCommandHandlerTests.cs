using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RecordPrepaymentForfeited;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class RecordPrepaymentForfeitedCommandHandlerTests
{
    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();

        public int GetByUserIdCallCount { get; private set; }
        public int GetOrCreateCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public Exception? ExceptionOnSave { get; set; }

        public void SeedWallet(Wallet wallet) => _wallets[wallet.UserId] = wallet;
        public int WalletCount => _wallets.Count;

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_wallets.Values.FirstOrDefault(w => w.Id == id));

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            GetByUserIdCallCount++;
            _wallets.TryGetValue(userId, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            GetOrCreateCallCount++;
            throw new InvalidOperationException("GetOrCreateByUserIdAsync must never be called when recording forfeitures.");
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
            SaveChangesCallCount++;
            if (ExceptionOnSave is not null)
            {
                throw ExceptionOnSave;
            }
            return Task.CompletedTask;
        }
    }

    private readonly FakeWalletRepository _walletRepository = new();

    private RecordPrepaymentForfeitedCommandHandler CreateHandler() =>
        new(_walletRepository, NullLogger<RecordPrepaymentForfeitedCommandHandler>.Instance);

    private static Wallet SeedDepositedWallet(
        FakeWalletRepository repo,
        Guid userId,
        Guid reservationId,
        decimal topUpAmount,
        decimal depositAmount,
        LedgerEntryType depositType = LedgerEntryType.ReservationDeposit)
    {
        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, topUpAmount, "Topup");
        wallet.AddEntry(
            depositType,
            -depositAmount,
            "Deposit",
            ReferenceType.Reservation,
            reservationId.ToString(),
            createdBy: userId);
        repo.SeedWallet(wallet);
        return wallet;
    }

    [Fact]
    public async Task Handle_WhenForfeitedAmountIsZero_ReturnsSuccessGuidEmpty_WithoutTouchRepo()
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "CustomerCancelled",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value);
        Assert.Equal(0, _walletRepository.GetByUserIdCallCount);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-20000)]
    [InlineData(20000.5)]
    [InlineData(1_000_000_000_000)]
    public async Task Handle_WhenForfeitedAmountInvalid_ReturnsAmountInvalid(decimal amount)
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.AmountInvalid, result.Error);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WhenReservationIdEmpty_ReturnsReservationIdInvalid()
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ReservationIdInvalid, result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdEmpty_ReturnsUserIdInvalid()
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.UserIdInvalid, result.Error);
    }

    [Fact]
    public async Task Handle_WhenLotIdEmpty_ReturnsLotIdInvalid()
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.LotIdInvalid, result.Error);
    }

    [Fact]
    public async Task Handle_WhenWalletNotFound_ReturnsWalletNotFound_AndNeverCreatesWallet()
    {
        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.WalletNotFound, result.Error);
        Assert.Equal(0, _walletRepository.GetOrCreateCallCount);
        Assert.Equal(0, _walletRepository.WalletCount);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WhenReservationNeverDebited_ReturnsReservationNotDebited()
    {
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Topup");
        _walletRepository.SeedWallet(wallet);

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ReservationNotDebited, result.Error);
        Assert.Equal(100_000m, wallet.Balance);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WhenDepositBelongsToDifferentUserWallet_ReturnsReservationNotDebited()
    {
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        SeedDepositedWallet(_walletRepository, ownerUserId, reservationId, 100_000m, 20_000m);
        var otherWallet = Wallet.Create(otherUserId);
        _walletRepository.SeedWallet(otherWallet);

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            otherUserId,
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ReservationNotDebited, result.Error);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(LedgerEntryType.Refund)]
    [InlineData(LedgerEntryType.PrepaymentApplied)]
    public async Task Handle_WhenPrepaymentAlreadySettled_ReturnsPrepaymentAlreadySettled(LedgerEntryType settlementType)
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = SeedDepositedWallet(_walletRepository, userId, reservationId, 100_000m, 20_000m);
        wallet.AddEntry(
            settlementType,
            20_000m,
            "Settled",
            ReferenceType.Reservation,
            reservationId.ToString());

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.PrepaymentAlreadySettled, result.Error);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_WhenForfeitedAmountExceedsRemainingUnsettled_ReturnsForfeitAmountExceedsDeposit()
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var wallet = SeedDepositedWallet(_walletRepository, userId, reservationId, 100_000m, 60_000m);
        wallet.AddEntry(
            LedgerEntryType.Refund,
            40_000m,
            "Partial refund",
            ReferenceType.Reservation,
            reservationId.ToString());

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            Guid.NewGuid(),
            30_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationDepositErrors.ForfeitAmountExceedsDeposit, result.Error);
        Assert.Equal(0, _walletRepository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(LedgerEntryType.ReservationDeposit)]
    [InlineData(LedgerEntryType.Prepayment)]
    public async Task Handle_WhenValidOnActiveWallet_RecordsEntryWithoutChangingBalance(LedgerEntryType depositType)
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var cancelledAtLocal = new DateTimeOffset(2026, 11, 20, 0, 10, 0, TimeSpan.FromHours(7));
        var wallet = SeedDepositedWallet(_walletRepository, userId, reservationId, 100_000m, 20_000m, depositType);

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            lotId,
            20_000m,
            "NoShow",
            cancelledAtLocal);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(80_000m, wallet.Balance);
        Assert.Equal(1, _walletRepository.SaveChangesCallCount);
        Assert.Equal(0, _walletRepository.GetOrCreateCallCount);

        var entry = Assert.Single(wallet.LedgerEntries, e => e.EntryType == LedgerEntryType.PrepaymentForfeited);
        Assert.Equal(entry.Id, result.Value);
        Assert.Equal(20_000m, entry.Amount);
        Assert.Equal(80_000m, entry.BalanceAfter);
        Assert.Equal(lotId, entry.LotId);
        Assert.Equal(ReferenceType.Reservation, entry.ReferenceType);
        Assert.Equal(reservationId.ToString(), entry.ReferenceId);
        Assert.Equal($"res-forfeit-{reservationId}", entry.IdempotencyKey);
        Assert.Null(entry.CreatedBy);
        Assert.Equal(cancelledAtLocal.ToUniversalTime(), entry.CreatedAt);
        Assert.Equal(TimeSpan.Zero, entry.CreatedAt.Offset);
    }

    [Fact]
    public async Task Handle_WhenValidOnClosedWallet_RecordsEntry_AndKeepsBalanceZero()
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var cancelledAtUtc = new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero);
        var wallet = SeedDepositedWallet(_walletRepository, userId, reservationId, 20_000m, 20_000m);
        wallet.Close();

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            lotId,
            20_000m,
            "NoShow",
            cancelledAtUtc);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, wallet.Balance);
        Assert.Equal(WalletStatus.Closed, wallet.Status);
        var entry = Assert.Single(wallet.LedgerEntries, e => e.EntryType == LedgerEntryType.PrepaymentForfeited);
        Assert.Equal(20_000m, entry.Amount);
        Assert.Equal(0m, entry.BalanceAfter);
        Assert.Equal(lotId, entry.LotId);
    }

    [Fact]
    public async Task Handle_WhenCancelReasonBlankOrTooLong_NormalizesAndClampsTo500Chars()
    {
        var userId = Guid.NewGuid();
        var resBlank = Guid.NewGuid();
        var resLong = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var wallet = SeedDepositedWallet(_walletRepository, userId, resBlank, 100_000m, 20_000m);
        wallet.AddEntry(
            LedgerEntryType.ReservationDeposit,
            -20_000m,
            "Deposit 2",
            ReferenceType.Reservation,
            resLong.ToString(),
            createdBy: userId);

        var handler = CreateHandler();

        var r1 = await handler.Handle(new RecordPrepaymentForfeitedCommand(
            resBlank, userId, lotId, 20_000m, "   ", DateTimeOffset.UtcNow), CancellationToken.None);
        var r2 = await handler.Handle(new RecordPrepaymentForfeitedCommand(
            resLong, userId, lotId, 20_000m, new string('X', 600), DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);

        var entryBlank = wallet.LedgerEntries.Single(e => e.ReferenceId == resBlank.ToString() && e.EntryType == LedgerEntryType.PrepaymentForfeited);
        Assert.EndsWith("(Unknown)", entryBlank.Reason);

        var entryLong = wallet.LedgerEntries.Single(e => e.ReferenceId == resLong.ToString() && e.EntryType == LedgerEntryType.PrepaymentForfeited);
        Assert.Equal(500, entryLong.Reason!.Length);
    }

    [Fact]
    public async Task Handle_WhenDuplicateEventReceived_ReturnsExistingEntryId_WithoutCallingSaveChanges()
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var wallet = SeedDepositedWallet(_walletRepository, userId, reservationId, 100_000m, 60_000m);

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            lotId,
            60_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, _walletRepository.SaveChangesCallCount);
        Assert.Single(wallet.LedgerEntries, e => e.EntryType == LedgerEntryType.PrepaymentForfeited);
        Assert.Equal(40_000m, wallet.Balance);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrowsConcurrencyException_PropagatesUnhandled()
    {
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        SeedDepositedWallet(_walletRepository, userId, reservationId, 100_000m, 20_000m);
        _walletRepository.ExceptionOnSave = new InvalidOperationException("Simulated DbUpdateConcurrencyException");

        var handler = CreateHandler();
        var command = new RecordPrepaymentForfeitedCommand(
            reservationId,
            userId,
            Guid.NewGuid(),
            20_000m,
            "NoShow",
            DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("Simulated DbUpdateConcurrencyException", ex.Message);
    }
}
