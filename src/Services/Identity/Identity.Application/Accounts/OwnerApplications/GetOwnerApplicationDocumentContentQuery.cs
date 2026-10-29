using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record GetOwnerApplicationDocumentContentQuery(
    Guid AccountId,
    string DocumentType) : IRequest<Result<OwnerDocumentContent>>;

/// <summary>The caller owns <see cref="Content"/> and must dispose it.</summary>
public sealed record OwnerDocumentContent(
    Stream Content,
    string DocumentType,
    string ContentType,
    string FileName,
    long SizeBytes);

public sealed class GetOwnerApplicationDocumentContentQueryHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents,
    IOwnerDocumentStore store,
    IOwnerDocumentRequirements requirements) : IRequestHandler<GetOwnerApplicationDocumentContentQuery, Result<OwnerDocumentContent>>
{
    public async Task<Result<OwnerDocumentContent>> Handle(GetOwnerApplicationDocumentContentQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.OwnerApplicationStatus is null)
            return Result<OwnerDocumentContent>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        // The type comes from the URL: only a name on a checklist gets as far as the repository, and the store is addressed by the
        // key of the row, never by anything the caller sent. Any checklist, not only the current one: a document kept after the
        // owner type changed is still the applicant's.
        var documentType = requirements.ResolveKnownDocumentType(request.DocumentType);
        if (documentType is null)
            return Result<OwnerDocumentContent>.Failure(OwnerApplicationErrors.DocumentNotFound);

        var document = await documents.FindByAccountAndTypeAsync(account.Id, documentType, cancellationToken);
        if (document is null)
            return Result<OwnerDocumentContent>.Failure(OwnerApplicationErrors.DocumentNotFound);

        if (document.PurgedAtUtc is not null)
            return Result<OwnerDocumentContent>.Failure(OwnerApplicationErrors.DocumentFileUnavailable);

        var content = await store.OpenReadAsync(document.StorageKey, cancellationToken);
        if (content is null)
            return Result<OwnerDocumentContent>.Failure(OwnerApplicationErrors.DocumentFileUnavailable);

        return Result<OwnerDocumentContent>.Success(new OwnerDocumentContent(
            content,
            document.DocumentType,
            document.ContentType,
            document.OriginalFileName,
            document.SizeBytes));
    }
}
