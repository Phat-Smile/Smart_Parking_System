using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.UnlockAccount;

public sealed record UnlockAccountCommand(Guid AccountId) : IRequest<Result<UnlockedAccount>>;

public sealed record UnlockedAccount(Guid AccountId, string Message);

public static class UnlockAccountErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "The account does not exist.");
}

// US-AUTH-008 (decided 2026-10-09): an administrator clears the 15-minute login lockout. Idempotent; the password
// and the sessions are left untouched.
public sealed class UnlockAccountCommandHandler(IAccountRepository accounts) : IRequestHandler<UnlockAccountCommand, Result<UnlockedAccount>>
{
    public async Task<Result<UnlockedAccount>> Handle(UnlockAccountCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<UnlockedAccount>.Failure(UnlockAccountErrors.AccountNotFound);

            account.RecordCorrectCredential(); // clears the failed-attempt streak and the lockout end
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<UnlockedAccount>.Success(new UnlockedAccount(account.Id, "Login lockout cleared."));
        }, cancellationToken);
    }
}
