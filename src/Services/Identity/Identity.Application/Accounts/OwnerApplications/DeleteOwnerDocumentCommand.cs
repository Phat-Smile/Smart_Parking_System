using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record DeleteOwnerDocumentCommand(
    Guid AccountId,
    string DocumentType) : IRequest<Result<bool>>;

public sealed class DeleteOwnerDocumentCommandHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents,
    IOwnerDocumentStore store,
    IOwnerDocumentRequirements requirements) : IRequestHandler<DeleteOwnerDocumentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteOwnerDocumentCommand request, CancellationToken cancellationToken)
    {
        string? removedStorageKey = null;
        var result = await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Same lock as the upload: the row lock is the first read, so a delete cannot interleave with an upload or a decision.
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null || account.OwnerApplicationStatus is null)
                return Result<bool>.Failure(OwnerApplicationErrors.ApplicationNotFound);

            if (account.OwnerApplicationStatus == OwnerApplicationStatus.Approved)
                return Result<bool>.Failure(OwnerApplicationErrors.ApplicationLocked);

            // Any checklist, not only the current one: after the owner type changed, a kept document is outside the new checklist.
            var docType = requirements.ResolveKnownDocumentType(request.DocumentType);
            if (docType is null)
                return Result<bool>.Failure(OwnerApplicationErrors.DocumentNotFound);

            var existing = await documents.FindByAccountAndTypeAsync(account.Id, docType, cancellationToken);
            if (existing is null)
                return Result<bool>.Failure(OwnerApplicationErrors.DocumentNotFound);

            removedStorageKey = existing.StorageKey;
            documents.Remove(existing);
            await documents.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }, cancellationToken);

        // Only after the commit: a failed commit must leave the file in place for the row that still points at it.
        if (result.IsSuccess && removedStorageKey is not null)
            await store.TryDeleteAsync(removedStorageKey);

        return result;
    }
}
