using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record GetOwnerApplicationDocumentsQuery(
    Guid AccountId) : IRequest<Result<OwnerApplicationDocumentsDto>>;

/// <summary>
/// What the admin reviews: every uploaded document, and the verdict on the checklist of the chosen owner type. Without an owner
/// type there is no checklist, so the application is not complete and no missing type is named.
/// </summary>
public sealed record OwnerApplicationDocumentsDto(
    Guid AccountId,
    string Status,
    string? OwnerType,
    bool IsComplete,
    IReadOnlyList<string> MissingDocumentTypes,
    IReadOnlyList<OwnerDocumentDto> Documents);

public sealed class GetOwnerApplicationDocumentsQueryHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents,
    IOwnerDocumentRequirements requirements) : IRequestHandler<GetOwnerApplicationDocumentsQuery, Result<OwnerApplicationDocumentsDto>>
{
    public async Task<Result<OwnerApplicationDocumentsDto>> Handle(GetOwnerApplicationDocumentsQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.OwnerApplicationStatus is null)
            return Result<OwnerApplicationDocumentsDto>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        var uploaded = await documents.ListByAccountAsync(account.Id, cancellationToken);

        // The same verdict the approval applies.
        IReadOnlyList<string> missing = [];
        var isComplete = false;
        if (account.OwnerType is { } ownerType && Enum.IsDefined(ownerType))
        {
            missing = requirements.GetMissingRequiredDocuments(ownerType, uploaded.Select(document => document.DocumentType));
            isComplete = missing.Count == 0;
        }

        return Result<OwnerApplicationDocumentsDto>.Success(new OwnerApplicationDocumentsDto(
            account.Id,
            account.OwnerApplicationStatus.Value.ToString(),
            account.OwnerType?.ToString(),
            isComplete,
            missing,
            uploaded.Select(document => new OwnerDocumentDto(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.UploadedAtUtc)).ToList()));
    }
}
