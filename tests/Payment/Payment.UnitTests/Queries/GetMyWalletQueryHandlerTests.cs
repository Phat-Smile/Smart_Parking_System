using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public class GetMyWalletQueryHandlerTests
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
            if (_wallets.TryGetValue(userId, out var wallet))
            {
                return Task.FromResult<Wallet?>(wallet);
            }

            return Task.FromResult<Wallet?>(null);
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
        public readonly List<VnpayTransaction> Transactions = [];

        public Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.TxnRef == txnRef));

        public Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(Guid walletId, string clientRequestKey, CancellationToken cancellationToken) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.WalletId == walletId && t.ClientRequestKey == clientRequestKey));

        public Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<VnpayTransaction?> GetTopupByTxnRefAndUserIdAsync(string txnRef, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Transactions.FirstOrDefault(t => t.TxnRef == txnRef));

        public Task<IReadOnlyList<VnpayTransaction>> GetRecentPendingByWalletIdAsync(Guid walletId, int limit, CancellationToken cancellationToken)
        {
            var items = Transactions
                .Where(t => t.WalletId == walletId && t.Status == VnpayTransactionStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<VnpayTransaction>>(items);
        }
    }

    private readonly FakeWalletRepository _fakeRepository;
    private readonly FakeVnpayTransactionRepository _fakeTxnRepository;
    private readonly IOptions<WalletOptions> _walletOptions;
    private readonly IOptions<VnpayOptions> _vnpayOptions;

    public GetMyWalletQueryHandlerTests()
    {
        _fakeRepository = new FakeWalletRepository();
        _fakeTxnRepository = new FakeVnpayTransactionRepository();
        _walletOptions = Options.Create(new WalletOptions
        {
            MinTopUpVnd = 10000m,
            MaxTopUpVnd = 5000000m,
            MaxBalanceVnd = 10000000m
        });
        _vnpayOptions = Options.Create(new VnpayOptions
        {
            Mode = "Sandbox"
        });
    }

    private GetMyWalletQueryHandler CreateHandler() =>
        new(_fakeRepository, _fakeTxnRepository, _walletOptions, _vnpayOptions);

    [Fact]
    public async Task Handle_WhenCustomerHasNoWallet_ShouldReturnZeroBalanceAndEmptyPendingTopUps()
    {
        // Arrange
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var query = new GetMyWalletQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert (TC-WAL-01 & TC-WAL-11)
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value.WalletId);
        Assert.Equal(0m, result.Value.Balance);
        Assert.Equal("VND", result.Value.Currency);
        Assert.True(result.Value.IsDemo);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.DemoNotice));
        Assert.Empty(result.Value.PendingTopUps);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasActiveWallet_ShouldReturnCorrectBalanceAndWalletId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        wallet.AddEntry(LedgerEntryType.TopUp, 150000m);
        _fakeRepository.SeedWallet(wallet);

        var handler = CreateHandler();
        var query = new GetMyWalletQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert (TC-WAL-02)
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(wallet.Id, result.Value.WalletId);
        Assert.Equal(150000m, result.Value.Balance);
        Assert.Equal("VND", result.Value.Currency);
    }

    [Fact]
    public async Task Handle_ShouldReturnLimitsAndGatewayModeFromConfiguration()
    {
        // Arrange
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var query = new GetMyWalletQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert (TC-WAL-06 & TC-WAL-07)
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(10000m, result.Value.Limits.MinTopUp);
        Assert.Equal(5000000m, result.Value.Limits.MaxTopUp);
        Assert.Equal(10000000m, result.Value.Limits.MaxBalance);
        Assert.Equal("Sandbox", result.Value.GatewayMode);
    }

    [Fact]
    public async Task Handle_ShouldAlwaysIncludeNfr11DemoNoticeAndIsDemoTrue()
    {
        // Arrange
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var query = new GetMyWalletQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert (US-WAL-007 / NFR-11)
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsDemo);
        Assert.Contains("sandbox", result.Value.DemoNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ShouldReturnFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var query = new GetMyWalletQuery(Guid.Empty);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("payment.invalid_user", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasPendingTopUps_ShouldReturnRecentPendingTopUps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        _fakeRepository.SeedWallet(wallet);

        var txn1 = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            wallet.Id,
            50000m,
            "req-key-1",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(10));

        var txn2 = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            wallet.Id,
            100000m,
            "req-key-2",
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow.AddMinutes(13));

        _fakeTxnRepository.Transactions.Add(txn1);
        _fakeTxnRepository.Transactions.Add(txn2);

        var handler = CreateHandler();
        var query = new GetMyWalletQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.PendingTopUps.Count);
        Assert.Equal(txn2.TxnRef, result.Value.PendingTopUps[0].TopupId); // Mới nhất lên đầu
        Assert.Equal(100000m, result.Value.PendingTopUps[0].Amount);
    }
}
