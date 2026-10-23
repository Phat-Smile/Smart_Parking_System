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
    IOwnerDocumentStore store) : IRequestHandler<DeleteOwnerDocumentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteOwnerDocumentCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.OwnerApplicationStatus is null)
            return Result<bool>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        if (account.OwnerApplicationStatus == OwnerApplicationStatus.Approved)
            return Result<bool>.Failure(OwnerApplicationErrors.ApplicationLocked);

        var docType = request.DocumentType?.Trim() ?? string.Empty;
        var existing = await documents.FindByAccountAndTypeAsync(account.Id, docType, cancellationToken);
        if (existing is null)
            return Result<bool>.Failure(OwnerApplicationErrors.DocumentNotFound);

        var storageKey = existing.StorageKey;
        documents.Remove(existing);
        await documents.SaveChangesAsync(cancellationToken);

        _ = store.DeleteAsync(storageKey, CancellationToken.None);

        return Result<bool>.Success(true);
    }
}
