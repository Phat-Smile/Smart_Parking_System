using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public class CreateWalletTopupCommandHandlerTests
{
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

    private class FakeVnpayTransactionRepository : IVnpayTransactionRepository
    {
        private readonly List<VnpayTransaction> _transactions = new();

        public void SeedTransaction(VnpayTransaction tx)
        {
            _transactions.Add(tx);
        }

        public Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken)
        {
            var tx = _transactions.FirstOrDefault(t => t.TxnRef == txnRef);
            return Task.FromResult(tx);
        }

        public Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(
            Guid walletId,
            string clientRequestKey,
            CancellationToken cancellationToken)
        {
            var tx = _transactions.FirstOrDefault(t => t.WalletId == walletId && t.ClientRequestKey == clientRequestKey);
            return Task.FromResult(tx);
        }

        public Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
        {
            _transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class FakePaymentGateway : IPaymentGateway
    {
        public string CreatePaymentUrl(string txnRef, decimal amount, DateTimeOffset expiresAtUtc, string clientIp)
        {
            return $"https://sandbox.vnpayment.vn/test?vnp_TxnRef={txnRef}&amount={amount}";
        }

        public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters)
        {
            return true;
        }
    }

    private readonly FakeWalletRepository _walletRepo = new();
    private readonly FakeVnpayTransactionRepository _txRepo = new();
    private readonly FakePaymentGateway _paymentGateway = new();
    private readonly IOptions<WalletOptions> _walletOptions = Options.Create(new WalletOptions
    {
        MinTopUpVnd = 10_000m,
        MaxTopUpVnd = 5_000_000m,
        MaxBalanceVnd = 10_000_000m
    });
    private readonly IOptions<VnpayOptions> _vnpayOptions = Options.Create(new VnpayOptions
    {
        PaymentExpiryMinutes = 15
    });

    private CreateWalletTopupCommandHandler CreateHandler()
    {
        return new CreateWalletTopupCommandHandler(
            _walletRepo,
            _txRepo,
            _paymentGateway,
            _walletOptions,
            _vnpayOptions);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsSuccessWithPaymentUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 100_000m,
            IdempotencyKey: "valid-key-12345678",
            ClientIp: "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100_000m, result.Value.Amount);
        Assert.Equal("Pending", result.Value.Status);
        Assert.False(result.Value.IsReplay);
        Assert.Contains(result.Value.TopupId, result.Value.PaymentUrl);
    }

    [Fact]
    public async Task Handle_WhenWalletDoesNotExist_CreatesWalletAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 50_000m,
            IdempotencyKey: "new-user-topup-key-1",
            ClientIp: "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var createdWallet = await _walletRepo.GetByUserIdAsync(userId, CancellationToken.None);
        Assert.NotNull(createdWallet);
        Assert.Equal(0m, createdWallet.Balance);
    }

    [Fact]
    public async Task Handle_WhenBalancePlusAmountExceedsMaxBalance_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 9_950_000m, "Seed balance");
        _walletRepo.SeedWallet(wallet);

        var handler = CreateHandler();
        var command = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 100_000m,
            IdempotencyKey: "exceed-balance-key-1",
            ClientIp: "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("payment.balance_limit_exceeded", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenReplayedWithSameKeyAndAmount_ReturnsExistingTransaction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 200_000m,
            IdempotencyKey: "replay-test-key-12345",
            ClientIp: "127.0.0.1");

        // Act - Call 1 (Create)
        var firstResult = await handler.Handle(command, CancellationToken.None);

        // Act - Call 2 (Replay)
        var secondResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(firstResult.IsSuccess);
        Assert.False(firstResult.Value!.IsReplay);

        Assert.True(secondResult.IsSuccess);
        Assert.True(secondResult.Value!.IsReplay);
        Assert.Equal(firstResult.Value.TopupId, secondResult.Value.TopupId);
    }

    [Fact]
    public async Task Handle_WhenSameKeyReusedWithDifferentAmount_ReturnsConflictFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var command1 = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 100_000m,
            IdempotencyKey: "conflict-key-123456",
            ClientIp: "127.0.0.1");

        var command2 = new CreateWalletTopupCommand(
            UserId: userId,
            Amount: 200_000m,
            IdempotencyKey: "conflict-key-123456",
            ClientIp: "127.0.0.1");

        // Act
        await handler.Handle(command1, CancellationToken.None);
        var result = await handler.Handle(command2, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("payment.idempotency_key_reused", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new CreateWalletTopupCommand(
            UserId: Guid.Empty,
            Amount: 100_000m,
            IdempotencyKey: "key-with-empty-user",
            ClientIp: "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("payment.invalid_user", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenReplayed_WhenAlreadySuccess_ReturnsSuccessWithoutNewPaymentUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var key = "replay-success-key";
        var wallet = await _walletRepo.GetOrCreateByUserIdAsync(userId, CancellationToken.None);

        var tx = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            wallet.Id,
            100_000m,
            key,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(10));
        tx.MarkAsSuccess("VNP123456", "NCB", "00", DateTimeOffset.UtcNow);
        _txRepo.SeedTransaction(tx);

        var command = new CreateWalletTopupCommand(userId, 100_000m, key, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsReplay);
        Assert.Equal("Success", result.Value.Status);
        Assert.Null(result.Value.PaymentUrl);
    }

    [Fact]
    public async Task Handle_WhenReplayed_WhenExpired_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var key = "replay-expired-key";
        var wallet = await _walletRepo.GetOrCreateByUserIdAsync(userId, CancellationToken.None);

        var tx = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            wallet.Id,
            100_000m,
            key,
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-5));
        _txRepo.SeedTransaction(tx);

        var command = new CreateWalletTopupCommand(userId, 100_000m, key, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("payment.topup_expired", result.Error!.Code);
    }
}
