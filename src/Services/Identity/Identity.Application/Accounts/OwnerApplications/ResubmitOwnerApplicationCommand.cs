using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ResubmitOwnerApplicationCommand(
    Guid AccountId,
    string? TaxCode = null,
    string? BusinessName = null,
    OwnerType? OwnerType = null) : IRequest<Result<OwnerApplicationActionResult>>;

public sealed class ResubmitOwnerApplicationCommandHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents,
    IOwnerDocumentRequirements requirements,
    IClock clock) : IRequestHandler<ResubmitOwnerApplicationCommand, Result<OwnerApplicationActionResult>>
{
    public async Task<Result<OwnerApplicationActionResult>> Handle(ResubmitOwnerApplicationCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null || account.OwnerApplicationStatus is null)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.ApplicationNotFound);

            if (account.OwnerApplicationStatus != OwnerApplicationStatus.Rejected)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.NotRejected);

            var effectiveOwnerType = request.OwnerType ?? account.OwnerType;
            if (!effectiveOwnerType.HasValue)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.OwnerTypeRequired);

            // Request or stored value alike: a number that is not an owner type has no checklist, so it must never pass as "complete".
            if (!Enum.IsDefined(effectiveOwnerType.Value))
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.OwnerTypeInvalid);

            // Check if all required documents for this ownerType have been uploaded
            var uploadedDocs = await documents.ListByAccountAsync(account.Id, cancellationToken);
            var missingDocs = requirements.GetMissingRequiredDocuments(
                effectiveOwnerType.Value,
                uploadedDocs.Select(d => d.DocumentType));

            if (missingDocs.Count > 0)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.DocumentsIncomplete);

            // A rejected application keeps its tax code only to fill the form again, and another application may have taken
            // the code since. Check the code that will be in force: the new one if it was sent, else the one the application carries.
            var effectiveTaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? account.OwnerTaxCode?.Trim() : request.TaxCode.Trim();
            if (!string.IsNullOrEmpty(effectiveTaxCode)
                && await accounts.OwnerTaxCodeExistsAsync(effectiveTaxCode, account.Id, cancellationToken))
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.TaxCodeAlreadyRegistered);

            account.ResubmitOwnerApplication(clock.UtcNow, request.TaxCode, request.BusinessName, effectiveOwnerType);
            // Two applications racing for one tax code both pass the check above; the unique index stops the loser.
            if (!await accounts.TrySaveOwnerApplicationAsync(cancellationToken))
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.TaxCodeAlreadyRegistered);

            return Result<OwnerApplicationActionResult>.Success(new OwnerApplicationActionResult(
                account.Id,
                account.OwnerApplicationStatus.Value.ToString(),
                "Đã gửi lại yêu cầu xét duyệt hồ sơ chủ bãi thành công."));
        }, cancellationToken);
    }
}
