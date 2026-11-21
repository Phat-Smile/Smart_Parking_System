using System.Security.Authentication;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class CreateRefundRequestCommandHandlerTests
{
    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
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

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
        {
            _walletsByUserId[wallet.UserId] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRefundRequestRepository : IRefundRequestRepository
    {
        public readonly List<RefundRequest> Requests = new();
        public bool SaveChangesCalled { get; private set; }

        public Task AddAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

        public Task ReloadAsync(RefundRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<RefundRequestPage> ListAsync(RefundRequestListFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RefundRequestPage(Requests, Requests.Count, filter.Page, filter.PageSize));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public readonly List<AuditEntryInput> Entries = new();

        public Task WriteAsync(AuditEntryInput entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeRefundRequestRepository _refundRequestRepository = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);

    private CreateRefundRequestCommandHandler CreateHandler() =>
        new(_walletRepository, _refundRequestRepository, _auditWriter, _clock);

    [Fact]
    public async Task Customer_creates_refund_request_successfully()
    {
        // R-01: valid customer with wallet creates refund request
        var customerId = Guid.NewGuid();
        var wallet = Wallet.Create(customerId);
        _walletRepository.AddWallet(wallet);

        var handler = CreateHandler();
        var command = new CreateRefundRequestCommand(
            CallerId: customerId,
            CallerRole: "Customer",
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố trừ tiền trùng",
            ReferenceType: ReferenceType.ParkingSession,
            ReferenceId: "SES-999");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Requested", result.Value!.Status);
        Assert.Single(_refundRequestRepository.Requests);
        Assert.True(_refundRequestRepository.SaveChangesCalled);

        // Audit written
        Assert.Single(_auditWriter.Entries);
        var audit = _auditWriter.Entries[0];
        Assert.Equal(AuditActionCatalog.RefundRequestCreated, audit.ActionType);
        Assert.Equal(command.Reason, audit.Reason);
    }

    [Theory]
    [InlineData("ParkingLotOwner")]
    [InlineData("LotOwner")]
    [InlineData("Staff")]
    [InlineData("SystemAdmin")]
    [InlineData(null)]
    public async Task Non_customer_role_returns_customer_only_error(string? invalidRole)
    {
        // R-03: non-customer cannot create refund request
        var callerId = Guid.NewGuid();
        var wallet = Wallet.Create(callerId);
        _walletRepository.AddWallet(wallet);

        var handler = CreateHandler();
        var command = new CreateRefundRequestCommand(
            CallerId: callerId,
            CallerRole: invalidRole,
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.CustomerOnly.Code, result.Error!.Code);
        Assert.Empty(_refundRequestRepository.Requests);
    }

    [Fact]
    public async Task Customer_without_wallet_returns_wallet_not_found()
    {
        // R-04: customer has no wallet -> 404 payment.refund_request.wallet_not_found
        var customerId = Guid.NewGuid();
        // Do not add wallet to repository

        var handler = CreateHandler();
        var command = new CreateRefundRequestCommand(
            CallerId: customerId,
            CallerRole: "Customer",
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.WalletNotFound.Code, result.Error!.Code);
        Assert.Empty(_refundRequestRepository.Requests);
    }
}
