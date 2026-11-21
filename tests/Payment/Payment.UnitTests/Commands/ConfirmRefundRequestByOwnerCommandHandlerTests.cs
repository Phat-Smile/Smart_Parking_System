using Microsoft.EntityFrameworkCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class ConfirmRefundRequestByOwnerCommandHandlerTests
{
    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeMembershipGateway : IParkingLotMembershipGateway
    {
        public Result<ParkingLotMembershipInfo>? CannedResult { get; set; }

        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId,
            Guid accountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CannedResult ?? Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner")));
    }

    private sealed class FakeRefundRequestRepository : IRefundRequestRepository
    {
        public RefundRequest? Request { get; set; }
        public bool ThrowConcurrencyOnSave { get; set; }
        public bool ReloadCalled { get; private set; }

        public Task AddAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Request?.Id == id ? Request : null);

        public Task ReloadAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            ReloadCalled = true;
            return Task.CompletedTask;
        }

        public Task<RefundRequestPage> ListAsync(RefundRequestListFilter filter, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrencyOnSave)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict");
            }
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

    private readonly FakeRefundRequestRepository _repository = new();
    private readonly FakeMembershipGateway _gateway = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);

    private ConfirmRefundRequestByOwnerCommandHandler CreateHandler() =>
        new(_repository, _gateway, _auditWriter, _clock);

    [Fact]
    public async Task Owner_confirms_successfully()
    {
        // R-05: valid owner confirms refund request
        var ownerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), lotId, 20000m, "Reason", Guid.NewGuid(), now: _clock.UtcNow);
        _repository.Request = request;

        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner"));

        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(request.Id, ownerId, "ParkingLotOwner");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Requested", result.Value!.Status);
        Assert.Equal(ownerId, result.Value.OwnerConfirmedBy);
        Assert.Equal(ownerId, request.OwnerConfirmedBy);

        Assert.Single(_auditWriter.Entries);
        Assert.Equal(AuditActionCatalog.RefundRequestOwnerConfirmed, _auditWriter.Entries[0].ActionType);
    }

    [Fact]
    public async Task Non_owner_role_returns_forbidden_role()
    {
        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(Guid.NewGuid(), Guid.NewGuid(), "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ForbiddenRole.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Request_not_found_returns_not_found()
    {
        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(Guid.NewGuid(), Guid.NewGuid(), "ParkingLotOwner");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.NotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Non_member_owner_returns_forbidden_lot_access()
    {
        // R-06: Owner does not own this specific lot
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 20000m, "Reason", Guid.NewGuid(), now: _clock.UtcNow);
        _repository.Request = request;

        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(false, "None"));

        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(request.Id, Guid.NewGuid(), "ParkingLotOwner");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ForbiddenLotAccess.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Gateway_unavailable_returns_service_unavailable()
    {
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 20000m, "Reason", Guid.NewGuid(), now: _clock.UtcNow);
        _repository.Request = request;

        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Failure(RefundRequestErrors.ParkingServiceUnavailable);

        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(request.Id, Guid.NewGuid(), "ParkingLotOwner");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ParkingServiceUnavailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Concurrency_conflict_reloads_entity_and_returns_conflict_error()
    {
        // R-10: Concurrency conflict triggers ReloadAsync and returns payment.concurrency_conflict
        var ownerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), lotId, 20000m, "Reason", Guid.NewGuid(), now: _clock.UtcNow);
        _repository.Request = request;
        _repository.ThrowConcurrencyOnSave = true;

        _gateway.CannedResult = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner"));

        var handler = CreateHandler();
        var command = new ConfirmRefundRequestByOwnerCommand(request.Id, ownerId, "ParkingLotOwner");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ConcurrencyConflict.Code, result.Error!.Code);
        Assert.True(_repository.ReloadCalled);
    }
}
