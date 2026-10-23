using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record UpdateOwnerTypeCommand(
    Guid AccountId,
    OwnerType OwnerType) : IRequest<Result<MyOwnerApplicationDto>>;

public sealed class UpdateOwnerTypeCommandHandler(
    IAccountRepository accounts) : IRequestHandler<UpdateOwnerTypeCommand, Result<MyOwnerApplicationDto>>
{
    public async Task<Result<MyOwnerApplicationDto>> Handle(UpdateOwnerTypeCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null || account.OwnerApplicationStatus is null)
                return Result<MyOwnerApplicationDto>.Failure(OwnerApplicationErrors.ApplicationNotFound);

            if (account.OwnerApplicationStatus == OwnerApplicationStatus.Approved)
                return Result<MyOwnerApplicationDto>.Failure(OwnerApplicationErrors.ApplicationLocked);

            account.SetOwnerType(request.OwnerType);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<MyOwnerApplicationDto>.Success(new MyOwnerApplicationDto(
                account.Id,
                account.Email,
                account.OwnerApplicationStatus.Value.ToString(),
                account.OwnerTaxCode,
                account.OwnerBusinessName,
                account.OwnerRejectionReason,
                account.OwnerReviewedAtUtc,
                account.OwnerType?.ToString()));
        }, cancellationToken);
    }
}
