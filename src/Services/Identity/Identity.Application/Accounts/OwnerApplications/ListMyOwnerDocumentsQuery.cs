using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ListMyOwnerDocumentsQuery(
    Guid AccountId) : IRequest<Result<IReadOnlyList<OwnerDocumentDto>>>;

public sealed class ListMyOwnerDocumentsQueryHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents) : IRequestHandler<ListMyOwnerDocumentsQuery, Result<IReadOnlyList<OwnerDocumentDto>>>
{
    public async Task<Result<IReadOnlyList<OwnerDocumentDto>>> Handle(ListMyOwnerDocumentsQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.OwnerApplicationStatus is null)
            return Result<IReadOnlyList<OwnerDocumentDto>>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        var list = await documents.ListByAccountAsync(request.AccountId, cancellationToken);
        var dtos = list.Select(d => new OwnerDocumentDto(
            d.Id,
            d.DocumentType,
            d.OriginalFileName,
            d.ContentType,
            d.SizeBytes,
            d.UploadedAtUtc)).ToList();

        return Result<IReadOnlyList<OwnerDocumentDto>>.Success(dtos);
    }
}
