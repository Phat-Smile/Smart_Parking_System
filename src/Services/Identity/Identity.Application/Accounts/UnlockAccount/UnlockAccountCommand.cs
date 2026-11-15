using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Auditing;

namespace ParkingSystem.Identity.Application.Accounts.UnlockAccount;

public sealed record UnlockAccountCommand(Guid AccountId, Guid? AdminId = null, string? IpAddress = null) : IRequest<Result<UnlockedAccount>>;

public sealed record UnlockedAccount(Guid AccountId, string Message);

public static class UnlockAccountErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại trong hệ thống.");
}

// US-AUTH-008 (decided 2026-10-09): an administrator clears the 15-minute login lockout. Idempotent; the password
// and the sessions are left untouched.
public sealed class UnlockAccountCommandHandler(
    IAccountRepository accounts,
    IClock clock,
    IIdentityAuditLog? auditLog = null) : IRequestHandler<UnlockAccountCommand, Result<UnlockedAccount>>
{
    public async Task<Result<UnlockedAccount>> Handle(UnlockAccountCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<UnlockedAccount>.Failure(UnlockAccountErrors.AccountNotFound);

            // Tells the admin whether a lockout was really lifted; an expired lockout counts as not locked.
            var wasLocked = account.LockoutEndUtc.HasValue && account.LockoutEndUtc.Value > clock.UtcNow;
            account.RecordCorrectCredential(); // clears the failed-attempt streak and the lockout end

            auditLog?.Record(
                actionType: "account.unlocked",
                actorId: request.AdminId,
                actorEmail: null,
                actorRole: "SystemAdmin",
                targetType: "account",
                targetId: account.Id.ToString(),
                reason: wasLocked ? "Mở khóa đăng nhập tài khoản (đã bị khóa do nhập sai)" : "Mở khóa và đặt lại số lần nhập sai",
                ipAddress: request.IpAddress);

            await accounts.SaveChangesAsync(cancellationToken);

            return Result<UnlockedAccount>.Success(new UnlockedAccount(account.Id, wasLocked
                ? "Đã mở khóa đăng nhập. Người dùng có thể đăng nhập lại ngay."
                : "Tài khoản không bị khóa đăng nhập; số lần nhập sai đã được đặt lại."));
        }, cancellationToken);
    }
}
