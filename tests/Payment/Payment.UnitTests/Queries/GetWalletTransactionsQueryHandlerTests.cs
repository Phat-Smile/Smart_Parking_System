using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public class GetWalletTransactionsQueryHandlerTests
{
    // Giả lập kho dữ liệu ví trong bộ nhớ cho test
    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();

        public void Seed(Wallet wallet) => _wallets[wallet.UserId] = wallet;

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.Values.FirstOrDefault(w => w.Id == id));

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_wallets.GetValueOrDefault(userId));

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!_wallets.TryGetValue(userId, out var wallet))
            {
                wallet = Wallet.Create(userId);
                _wallets[userId] = wallet;
            }
            return Task.FromResult(wallet);
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        {
            _wallets[wallet.UserId] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Giả lập kho truy vấn lịch sử giao dịch không tracking
    private class FakeWalletTransactionQueryRepository : IWalletTransactionQueryRepository
    {
        public List<WalletTransactionRawItem> ItemsToReturn { get; set; } = new();
        public int TotalCountToReturn { get; set; }
        public WalletTransactionFilter? LastFilterReceived { get; private set; }

        public Task<(IReadOnlyList<WalletTransactionRawItem> Items, int TotalCount)> GetTransactionsAsync(
            Guid walletId,
            WalletTransactionFilter filter,
            CancellationToken cancellationToken)
        {
            LastFilterReceived = filter;
            return Task.FromResult(((IReadOnlyList<WalletTransactionRawItem>)ItemsToReturn, TotalCountToReturn));
        }
    }

    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeWalletTransactionQueryRepository _queryRepository = new();
    private readonly GetWalletTransactionsQueryHandler _handler;

    public GetWalletTransactionsQueryHandlerTests()
    {
        _handler = new GetWalletTransactionsQueryHandler(_walletRepository, _queryRepository);
    }

    // 1. Kiểm tra vai trò tài khoản (chỉ Customer mới có ví)

    [Theory]
    [InlineData("ParkingLotOwner")]
    [InlineData("Staff")]
    [InlineData("SystemAdmin")]
    public async Task Handle_WhenRoleNotCustomer_ShouldReturnCustomerOnlyError(string role)
    {
        // Arrange
        var query = new GetWalletTransactionsQuery(Guid.NewGuid(), role, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(PaymentErrors.Wallet.CustomerOnly.Code, result.Error!.Code);
    }

    // 2. Kiểm tra tham số phân trang page

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999999999999")]
    public async Task Handle_WhenPageIsInvalid_ShouldReturnInvalidPageError(string invalidPage)
    {
        // Arrange
        var query = new GetWalletTransactionsQuery(Guid.NewGuid(), "Customer", invalidPage, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidPage.Code, result.Error!.Code);
    }

    // 3. Kiểm tra tham số pageSize (từ 1 đến 50)

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("51")]
    [InlineData("100")]
    [InlineData("xyz")]
    public async Task Handle_WhenPageSizeIsInvalid_ShouldReturnInvalidPageSizeError(string invalidSize)
    {
        // Arrange
        var query = new GetWalletTransactionsQuery(Guid.NewGuid(), "Customer", "1", invalidSize, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidPageSize.Code, result.Error!.Code);
    }

    // 4. Kiểm tra loại giao dịch không hợp lệ

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("TopUp,Refund")]
    [InlineData("999")]
    public async Task Handle_WhenTypeIsInvalid_ShouldReturnInvalidEntryTypeError(string invalidType)
    {
        // Arrange
        var query = new GetWalletTransactionsQuery(Guid.NewGuid(), "Customer", "1", "20", invalidType, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidEntryType.Code, result.Error!.Code);
    }

    // 5. Kiểm tra khoảng thời gian và định dạng UTC

    [Theory]
    [InlineData("2026-10-18T10:00:00+07:00")]
    [InlineData("yesterday")]
    public async Task Handle_WhenFromUtcIsInvalid_ShouldReturnInvalidDateRangeError(string invalidFrom)
    {
        // Arrange
        var query = new GetWalletTransactionsQuery(
            Guid.NewGuid(), "Customer", "1", "20", null, invalidFrom, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidDateRange.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenFromUtcGreaterThanOrEqualToToUtc_ShouldReturnInvalidDateRangeError()
    {
        // Arrange: fromUtc sau toUtc
        var query1 = new GetWalletTransactionsQuery(
            Guid.NewGuid(), "Customer", "1", "20", null, "2026-10-18T12:00:00Z", "2026-10-18T10:00:00Z");

        // Act 1
        var result1 = await _handler.Handle(query1, CancellationToken.None);

        // Assert 1
        Assert.True(result1.IsFailure);
        Assert.NotNull(result1.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidDateRange.Code, result1.Error!.Code);

        // Arrange: fromUtc bằng toUtc
        var query2 = new GetWalletTransactionsQuery(
            Guid.NewGuid(), "Customer", "1", "20", null, "2026-10-18T10:00:00Z", "2026-10-18T10:00:00Z");

        // Act 2
        var result2 = await _handler.Handle(query2, CancellationToken.None);

        // Assert 2
        Assert.True(result2.IsFailure);
        Assert.NotNull(result2.Error);
        Assert.Equal(PaymentErrors.Wallet.InvalidDateRange.Code, result2.Error!.Code);
    }

    // 6. Khách hàng chưa có ví trả về danh sách rỗng thành công

    [Fact]
    public async Task Handle_WhenUserHasNoWallet_ShouldReturnSuccessWithEmptyItems()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetWalletTransactionsQuery(userId, "Customer", "1", "20", null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalItems);
        Assert.Equal(0, result.Value.TotalPages);
    }

    // 7. Truy vấn thành công và kiểm tra quy tắc tính BalanceDelta

    [Fact]
    public async Task Handle_WhenValidQuery_ShouldQueryRepositoryAndMapItemsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        _walletRepository.Seed(wallet);

        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _queryRepository.ItemsToReturn = new List<WalletTransactionRawItem>
        {
            // Bút toán nạp tiền: tăng số dư ví
            new(entryId1, now, LedgerEntryType.TopUp, 50000m, 50000m, "Top-up via VNPay", ReferenceType.VnpayTransaction, "PAY-01", 1L),
            // Bút toán cọc giữ chỗ: chỉ ghi nhận thông tin, số dư ví không đổi
            new(entryId2, now.AddMinutes(5), LedgerEntryType.PrepaymentApplied, 15000m, 50000m, "Deducted deposit", ReferenceType.Reservation, "RES-01", 2L)
        };
        _queryRepository.TotalCountToReturn = 45;

        // Thử nghiệm case-insensitive cho type: "topup"
        var query = new GetWalletTransactionsQuery(userId, "Customer", "1", "20", "topup", "2026-10-18T00:00:00+00:00", "2026-10-19T00:00:00Z");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(45, result.Value.TotalItems);
        Assert.Equal(3, result.Value.TotalPages); // 45 items / 20 = 3 trang
        Assert.Equal(2, result.Value.Items.Count);

        // Item 1: TopUp ảnh hưởng số dư
        var item1 = result.Value.Items[0];
        Assert.Equal(50000L, item1.Amount);
        Assert.Equal(50000L, item1.BalanceDelta);
        Assert.True(item1.AffectsBalance);
        Assert.Equal("VnpayTransaction", item1.ReferenceType);
        Assert.Equal("PAY-01", item1.ReferenceId);

        // Item 2: PrepaymentApplied không ảnh hưởng số dư ví
        var item2 = result.Value.Items[1];
        Assert.Equal(15000L, item2.Amount);
        Assert.Equal(0L, item2.BalanceDelta);
        Assert.False(item2.AffectsBalance);
        Assert.Equal("Reservation", item2.ReferenceType);
        Assert.Equal("RES-01", item2.ReferenceId);

        // Kiểm tra filter truyền xuống Query Repository
        Assert.NotNull(_queryRepository.LastFilterReceived);
        Assert.Equal(LedgerEntryType.TopUp, _queryRepository.LastFilterReceived!.Type);
    }
}
