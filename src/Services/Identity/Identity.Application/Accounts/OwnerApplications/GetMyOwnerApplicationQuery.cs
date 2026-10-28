using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record GetMyOwnerApplicationQuery(
    Guid AccountId) : IRequest<Result<MyOwnerApplicationDto>>;

public sealed record MyOwnerApplicationDto(
    Guid AccountId,
    string Email,
    string? Status,
    string? TaxCode,
    string? BusinessName,
    string? RejectionReason,
    DateTimeOffset? ReviewedAtUtc,
    string? OwnerType);

public sealed class GetMyOwnerApplicationQueryHandler(
    IAccountRepository accounts) : IRequestHandler<GetMyOwnerApplicationQuery, Result<MyOwnerApplicationDto>>
{
    public async Task<Result<MyOwnerApplicationDto>> Handle(GetMyOwnerApplicationQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<MyOwnerApplicationDto>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        return Result<MyOwnerApplicationDto>.Success(new MyOwnerApplicationDto(
            account.Id,
            account.Email,
            account.OwnerApplicationStatus?.ToString(),
            account.OwnerTaxCode,
            account.OwnerBusinessName,
            account.OwnerRejectionReason,
            account.OwnerReviewedAtUtc,
            account.OwnerType?.ToString()));
    }
}
