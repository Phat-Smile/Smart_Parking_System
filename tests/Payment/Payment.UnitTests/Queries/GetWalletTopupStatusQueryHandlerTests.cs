using FluentValidation.TestHelper;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public class GetWalletTopupStatusQueryHandlerTests
{
    private class FakeVnpayTransactionRepository : IVnpayTransactionRepository
    {
        private readonly List<(VnpayTransaction Txn, Guid UserId)> _transactions = new();

        public void SeedTransaction(VnpayTransaction txn, Guid userId)
        {
            _transactions.Add((txn, userId));
        }

        public Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken)
        {
            var item = _transactions.FirstOrDefault(x => x.Txn.TxnRef == txnRef);
            return Task.FromResult(item.Txn);
        }

        public Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(
            Guid walletId,
            string clientRequestKey,
            CancellationToken cancellationToken)
        {
            var item = _transactions.FirstOrDefault(x => x.Txn.WalletId == walletId && x.Txn.ClientRequestKey == clientRequestKey);
            return Task.FromResult(item.Txn);
        }

        public Task<VnpayTransaction?> GetTopupByTxnRefAndUserIdAsync(
            string txnRef,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var item = _transactions.FirstOrDefault(x => x.Txn.TxnRef == txnRef && x.UserId == userId && x.Txn.Purpose == VnpayTransactionPurpose.WalletTopUp);
            return Task.FromResult(item.Txn);
        }

        public Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VnpayTransaction>> GetRecentPendingByWalletIdAsync(Guid walletId, int limit, CancellationToken cancellationToken)
        {
            var items = _transactions
                .Where(x => x.Txn.WalletId == walletId && x.Txn.Status == VnpayTransactionStatus.Pending)
                .OrderByDescending(x => x.Txn.CreatedAt)
                .Select(x => x.Txn)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<VnpayTransaction>>(items);
        }

        public Task<IReadOnlyList<VnpayTransaction>> GetRecentExpiredTopupsByWalletIdAsync(Guid walletId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VnpayTransaction>>(Array.Empty<VnpayTransaction>());

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private readonly FakeVnpayTransactionRepository _repo = new();
    private readonly GetWalletTopupStatusQueryValidator _validator = new();

    [Fact]
    public async Task Handle_WhenTransactionIsPending_ReturnsPendingStatus()
    {
        // Giao dich moi tao o trang thai dang cho
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var txnRef = "1234567890abcdef1234567890abcdef";
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddMinutes(15);

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            txnRef,
            walletId,
            100000m,
            "client_request_key_123",
            createdAt,
            expiresAt);

        _repo.SeedTransaction(txn, userId);

        var handler = new GetWalletTopupStatusQueryHandler(_repo);
        var query = new GetWalletTopupStatusQuery(userId, txnRef);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(txnRef, result.Value!.TopupId);
        Assert.Equal(100000, result.Value.Amount);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Null(result.Value.CompletedAtUtc);
    }

    [Fact]
    public async Task Handle_WhenPendingLinkHasExpired_ReturnsExpiredWithoutChangingTransaction()
    {
        var userId = Guid.NewGuid();
        var txnRef = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(), txnRef, Guid.NewGuid(), 50000m, "expired-request-key",
            createdAt, createdAt.AddMinutes(5));
        _repo.SeedTransaction(txn, userId);

        var result = await new GetWalletTopupStatusQueryHandler(_repo)
            .Handle(new GetWalletTopupStatusQuery(userId, txnRef), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Expired", result.Value!.Status);
        Assert.Equal(VnpayTransactionStatus.Pending, txn.Status);
    }

    [Fact]
    public async Task Handle_WhenLateVnpaySuccessArrives_ReturnsSucceededAfterExpiry()
    {
        var userId = Guid.NewGuid();
        var txnRef = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(), txnRef, Guid.NewGuid(), 50000m, "late-request-key",
            createdAt, createdAt.AddMinutes(5));
        txn.MarkAsSuccess("VNP123", "NCB", "00", DateTimeOffset.UtcNow);
        _repo.SeedTransaction(txn, userId);

        var result = await new GetWalletTopupStatusQueryHandler(_repo)
            .Handle(new GetWalletTopupStatusQuery(userId, txnRef), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value!.Status);
    }

    [Fact]
    public async Task Handle_WhenTransactionIsSuccess_ReturnsSucceededStatus()
    {
        // Giao dich da xac nhan thanh cong qua IPN
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var txnRef = "abcdef1234567890abcdef1234567890";
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresAt = createdAt.AddMinutes(15);
        var completedAt = DateTimeOffset.UtcNow;

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            txnRef,
            walletId,
            50000m,
            "client_request_key_456",
            createdAt,
            expiresAt);

        txn.MarkAsSuccess("VNP123456", "NCB", "00", completedAt);

        _repo.SeedTransaction(txn, userId);

        var handler = new GetWalletTopupStatusQueryHandler(_repo);
        var query = new GetWalletTopupStatusQuery(userId, txnRef);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value!.Status);
        Assert.Equal(completedAt, result.Value.CompletedAtUtc);
    }

    [Fact]
    public async Task Handle_WhenTransactionIsFailed_ReturnsFailedStatus()
    {
        // Giao dich bi that bai
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var txnRef = "abcdef1234567890abcdef1234567891";
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresAt = createdAt.AddMinutes(15);
        var completedAt = DateTimeOffset.UtcNow;

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            txnRef,
            walletId,
            50000m,
            "client_request_key_789",
            createdAt,
            expiresAt);

        txn.MarkAsFailed("VNP123457", "NCB", "24", completedAt);

        _repo.SeedTransaction(txn, userId);

        var handler = new GetWalletTopupStatusQueryHandler(_repo);
        var query = new GetWalletTopupStatusQuery(userId, txnRef);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value!.Status);
    }

    [Fact]
    public async Task Handle_WhenTransactionBelongsToAnotherUser_ReturnsNotFound()
    {
        // Bao ve IDOR khi nguoi dung khac tra cuu ma giao dich
        var ownerUserId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var txnRef = "abcdef1234567890abcdef1234567892";

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            txnRef,
            walletId,
            50000m,
            "client_request_key_999",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));

        _repo.SeedTransaction(txn, ownerUserId);

        var handler = new GetWalletTopupStatusQueryHandler(_repo);
        var query = new GetWalletTopupStatusQuery(callerUserId, txnRef);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.Topup.NotFound.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("12345")] // Ngan hon 32 ky tu
    [InlineData("1234567890abcdef1234567890abcdef123")] // Dai hon 32 ky tu
    [InlineData("1234567890abcdef1234567890abcdeg")] // Co ky tu g khong phai hex
    [InlineData("")] // Chuoi rong
    public void Validator_WhenTopupIdIsInvalid_HasValidationError(string invalidTopupId)
    {
        // Kiem tra dinh dang 32 ky tu hex
        var query = new GetWalletTopupStatusQuery(Guid.NewGuid(), invalidTopupId);
        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.TopupId)
            .WithErrorMessage(PaymentErrors.Topup.InvalidId.Message);
    }

    [Fact]
    public void Validator_WhenTopupIdIsValidHex32_PassesValidation()
    {
        // Kiem tra hop le dung 32 ky tu hex
        var query = new GetWalletTopupStatusQuery(Guid.NewGuid(), "1234567890abcdef1234567890abcdef");
        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.TopupId);
    }
}
