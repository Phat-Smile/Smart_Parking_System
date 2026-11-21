using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public sealed class ListRefundRequestsQueryHandlerTests
{
    private sealed class FakeMembershipGateway : IParkingLotMembershipGateway
    {
        public Result<ParkingLotMembershipInfo>? CannedResult { get; set; }

        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId,
            Guid accountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CannedResult ?? Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner")));
    }

    private sealed class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _walletsByUserId = new();

        public void AddWallet(Wallet wallet) => _walletsByUserId[wallet.UserId] = wallet;

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_walletsByUserId.Values.FirstOrDefault(w => w.Id == id));

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_walletsByUserId.GetValueOrDefault(userId));

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRefundRequestRepository : IRefundRequestRepository
    {
        public readonly List<RefundRequest> Items = new();
        public RefundRequestListFilter? LastFilter { get; private set; }

        public Task AddAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            Items.Add(request);
            return Task.CompletedTask;
        }

        public Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

        public Task ReloadAsync(RefundRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<RefundRequestPage> ListAsync(RefundRequestListFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            var query = Items.AsEnumerable();
            if (filter.Status.HasValue)
                query = query.Where(r => r.Status == filter.Status.Value);
            if (filter.LotId.HasValue)
                query = query.Where(r => r.LotId == filter.LotId.Value);
            if (filter.WalletId.HasValue)
                query = query.Where(r => r.WalletId == filter.WalletId.Value);

            var list = query.ToList();
            return Task.FromResult(new RefundRequestPage(list, list.Count, filter.Page, filter.PageSize));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private readonly FakeRefundRequestRepository _repository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeMembershipGateway _gateway = new();

    private ListRefundRequestsQueryHandler CreateHandler() =>
        new(_repository, _walletRepository, _gateway);

    [Fact]
    public async Task Admin_queries_without_restrictions()
    {
        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(Guid.NewGuid(), "SystemAdmin", Status: RefundStatus.Requested);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_repository.LastFilter);
        Assert.Null(_repository.LastFilter!.WalletId);
        Assert.Null(_repository.LastFilter.LotId);
        Assert.Equal(RefundStatus.Requested, _repository.LastFilter.Status);
    }

    [Fact]
    public async Task Owner_omitting_lot_id_returns_lot_id_required_400()
    {
        // Fix 1: Owner requires lotId
        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(Guid.NewGuid(), "ParkingLotOwner", LotId: null);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.LotIdRequired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Owner_querying_unowned_lot_returns_forbidden_403()
    {
        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(false, "None"));

        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(Guid.NewGuid(), "ParkingLotOwner", LotId: Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ForbiddenLotAccess.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Owner_querying_owned_lot_succeeds_and_filters_by_lot_id()
    {
        var ownerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner"));

        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(ownerId, "ParkingLotOwner", LotId: lotId);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, _repository.LastFilter!.LotId);
        Assert.Null(_repository.LastFilter.WalletId);
    }

    [Fact]
    public async Task Customer_is_filtered_by_own_wallet()
    {
        var customerId = Guid.NewGuid();
        var wallet = Wallet.Create(customerId);
        _walletRepository.AddWallet(wallet);

        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(customerId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(wallet.Id, _repository.LastFilter!.WalletId);
    }

    [Fact]
    public async Task Customer_without_wallet_returns_empty_page()
    {
        var customerId = Guid.NewGuid();
        var handler = CreateHandler();
        var query = new ListRefundRequestsQuery(customerId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }
}
