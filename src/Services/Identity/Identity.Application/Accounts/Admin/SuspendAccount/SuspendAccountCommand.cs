using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.Admin.SuspendAccount;

public sealed record SuspendAccountCommand(Guid AdminId, Guid AccountId) : IRequest<Result<SuspendedAccount>>;

public sealed record SuspendedAccount(Guid AccountId, string Message);

public static class SuspendAccountErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error CannotSuspendSelf =
        new("identity.admin_cannot_suspend_self", "Quản trị viên không thể tự đình chỉ tài khoản của mình.");

    public static readonly Error CannotSuspendAdmin =
        new("identity.cannot_suspend_admin", "Không thể đình chỉ tài khoản Quản trị viên khác.");

    public static readonly Error AccountAlreadySuspended =
        new("identity.account_already_suspended", "Tài khoản đã đang bị đình chỉ.");
}

public sealed class SuspendAccountCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IClock clock) : IRequestHandler<SuspendAccountCommand, Result<SuspendedAccount>>
{
    public async Task<Result<SuspendedAccount>> Handle(SuspendAccountCommand request, CancellationToken cancellationToken)
    {
        if (request.AdminId == request.AccountId)
            return Result<SuspendedAccount>.Failure(SuspendAccountErrors.CannotSuspendSelf);

        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<SuspendedAccount>.Failure(SuspendAccountErrors.AccountNotFound);

            if (account.Role == AccountRole.SystemAdmin)
                return Result<SuspendedAccount>.Failure(SuspendAccountErrors.CannotSuspendAdmin);

            if (account.Status == AccountStatus.Suspended)
                return Result<SuspendedAccount>.Failure(SuspendAccountErrors.AccountAlreadySuspended);

            account.Suspend();
            await authSessions.RevokeAllForAccountAsync(request.AccountId, now, "AccountSuspended", cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<SuspendedAccount>.Success(
                new SuspendedAccount(account.Id, "Tài khoản đã bị đình chỉ và toàn bộ phiên làm việc đã được thu hồi."));
        }, cancellationToken);
    }
}
