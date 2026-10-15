using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Admin.ActivateAccount;

public sealed record ActivateAccountCommand(Guid AccountId) : IRequest<Result<ActivatedAccount>>;

public sealed record ActivatedAccount(Guid AccountId, string Status, string Message);

public static class ActivateAccountErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error AccountNotSuspended =
        new("identity.account_not_suspended", "Tài khoản không đang bị đình chỉ.");
}

public sealed class ActivateAccountCommandHandler(IAccountRepository accounts)
    : IRequestHandler<ActivateAccountCommand, Result<ActivatedAccount>>
{
    public async Task<Result<ActivatedAccount>> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<ActivatedAccount>.Failure(ActivateAccountErrors.AccountNotFound);

            if (account.Status != AccountStatus.Suspended)
                return Result<ActivatedAccount>.Failure(ActivateAccountErrors.AccountNotSuspended);

            account.Activate();
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<ActivatedAccount>.Success(
                new ActivatedAccount(account.Id, account.Status.ToString(),
                    "Đã kích hoạt lại tài khoản. Người dùng có thể đăng nhập."));
        }, cancellationToken);
    }
}
