using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.LookupStaff;

public sealed record LookupStaffAccountByEmailQuery(string? Email) : IRequest<Result<StaffAccountLookupResult>>;

public sealed record StaffAccountLookupResult(bool IsFound, Guid AccountId);

public sealed class LookupStaffAccountByEmailQueryHandler(IAccountRepository accounts)
    : IRequestHandler<LookupStaffAccountByEmailQuery, Result<StaffAccountLookupResult>>
{
    public async Task<Result<StaffAccountLookupResult>> Handle(
        LookupStaffAccountByEmailQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<StaffAccountLookupResult>.Success(new StaffAccountLookupResult(false, Guid.Empty));
        }

        var normalizedEmail = Account.NormalizeEmail(request.Email);
        var account = await accounts.FindByEmailAsync(normalizedEmail, cancellationToken);

        // Anti-enumeration invariant: Return IsFound = true ONLY when:
        // 1. Account exists
        // 2. Account is active (Status == AccountStatus.EmailVerified)
        // 3. Account has Role == AccountRole.Staff
        // All other cases (Customer, Owner, SystemAdmin, suspended, disabled, unverified, not found)
        // return IsFound = false with empty guid to prevent account/email enumeration.
        if (account is not null &&
            account.Status == AccountStatus.EmailVerified &&
            account.Role == AccountRole.Staff)
        {
            return Result<StaffAccountLookupResult>.Success(new StaffAccountLookupResult(true, account.Id));
        }

        return Result<StaffAccountLookupResult>.Success(new StaffAccountLookupResult(false, Guid.Empty));
    }
}
