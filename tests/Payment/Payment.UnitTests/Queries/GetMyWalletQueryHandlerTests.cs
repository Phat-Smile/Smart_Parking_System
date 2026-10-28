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

    private readonly FakeWalletRepository _fakeRepository;
    private readonly IOptions<WalletOptions> _walletOptions;
    private readonly IOptions<VnpayOptions> _vnpayOptions;

    public GetMyWalletQueryHandlerTests()
    {
        _fakeRepository = new FakeWalletRepository();
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

    [Fact]
    public async Task Handle_WhenCustomerHasNoWallet_ShouldReturnZeroBalanceAndEmptyPendingTopUps()
    {
        // Arrange
        var handler = new GetMyWalletQueryHandler(_fakeRepository, _walletOptions, _vnpayOptions);
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

        var handler = new GetMyWalletQueryHandler(_fakeRepository, _walletOptions, _vnpayOptions);
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
        var handler = new GetMyWalletQueryHandler(_fakeRepository, _walletOptions, _vnpayOptions);
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
        var handler = new GetMyWalletQueryHandler(_fakeRepository, _walletOptions, _vnpayOptions);
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
        var handler = new GetMyWalletQueryHandler(_fakeRepository, _walletOptions, _vnpayOptions);
        var query = new GetMyWalletQuery(Guid.Empty);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("payment.invalid_user", result.Error.Code);
    }
}
