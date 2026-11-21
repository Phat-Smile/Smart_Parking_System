using Microsoft.EntityFrameworkCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public sealed class RejectRefundRequestCommandHandlerTests
{
    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
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
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);

    private RejectRefundRequestCommandHandler CreateHandler() =>
        new(_repository, _auditWriter, _clock);

    [Fact]
    public async Task Admin_rejects_confirmed_request_successfully()
    {
        // R-07, R-11: Admin rejects after owner confirmation, writes audit with rejection reason, ledger is null
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 25000m, "Reason", creatorId, now: _clock.UtcNow);
        request.ConfirmByOwner(ownerId, _clock.UtcNow);
        _repository.Request = request;

        var handler = CreateHandler();
        var command = new RejectRefundRequestCommand(request.Id, adminId, "SystemAdmin", "Không phải lỗi hệ thống");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", result.Value!.Status);
        Assert.Equal(adminId, result.Value.ReviewedBy);
        Assert.Equal(_clock.UtcNow, result.Value.ReviewedAt);
        Assert.Null(request.LedgerEntryId);

        // Audit log verified
        Assert.Single(_auditWriter.Entries);
        var audit = _auditWriter.Entries[0];
        Assert.Equal(AuditActionCatalog.RefundRequestRejected, audit.ActionType);
        Assert.Equal("Không phải lỗi hệ thống", audit.Reason);
    }

    [Fact]
    public async Task Creator_cannot_review_returns_403()
    {
        // R-08: Separation of duties
        var sameUserId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 25000m, "Reason", sameUserId, now: _clock.UtcNow);
        request.ConfirmByOwner(Guid.NewGuid(), _clock.UtcNow);
        _repository.Request = request;

        var handler = CreateHandler();
        var command = new RejectRefundRequestCommand(request.Id, sameUserId, "SystemAdmin", "Lý do");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.CreatorCannotReview.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Admin_reject_before_owner_confirmation_returns_owner_not_confirmed_409()
    {
        // R-09 / Decision D2: Admin cannot reject before owner confirmed
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 25000m, "Reason", creatorId, now: _clock.UtcNow);
        // Notice: OwnerConfirmedBy is null!
        _repository.Request = request;

        var handler = CreateHandler();
        var command = new RejectRefundRequestCommand(request.Id, adminId, "SystemAdmin", "Từ chối sớm");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.OwnerNotConfirmed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_role_returns_forbidden_role()
    {
        var handler = CreateHandler();
        var command = new RejectRefundRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Customer", "Lý do");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ForbiddenRole.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Concurrency_conflict_reloads_entity_and_returns_conflict_error()
    {
        // R-10: Concurrency conflict handling on reject
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var request = RefundRequest.Create(Guid.NewGuid(), Guid.NewGuid(), 25000m, "Reason", creatorId, now: _clock.UtcNow);
        request.ConfirmByOwner(Guid.NewGuid(), _clock.UtcNow);
        _repository.Request = request;
        _repository.ThrowConcurrencyOnSave = true;

        var handler = CreateHandler();
        var command = new RejectRefundRequestCommand(request.Id, adminId, "SystemAdmin", "Từ chối");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundRequestErrors.ConcurrencyConflict.Code, result.Error!.Code);
        Assert.True(_repository.ReloadCalled);
    }
}
