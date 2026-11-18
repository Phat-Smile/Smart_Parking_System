using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Application.Profiles.CustomerMedia;

namespace ParkingSystem.Identity.Application.Accounts.AnonymizeAccount;

public sealed record AnonymizeAccountCommand(Guid AccountId) : IRequest<Result<AnonymizedAccountResult>>;

public sealed record AnonymizedAccountResult(string Message);

public static class AnonymizeAccountErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
    public static readonly Error AlreadyDisabled =
        new("identity.account_already_disabled", "Tài khoản đã bị vô hiệu hóa.");
}

public sealed class AnonymizeAccountCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    ICustomerMediaStore media,
    IAuthSessionRepository authSessions,
    IClock clock,
    IIdentityAuditLog auditLog) : IRequestHandler<AnonymizeAccountCommand, Result<AnonymizedAccountResult>>
{
    public async Task<Result<AnonymizedAccountResult>> Handle(
        AnonymizeAccountCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<AnonymizedAccountResult>.Failure(AnonymizeAccountErrors.AccountNotFound);

            if (account.Status == Domain.Accounts.AccountStatus.Disabled)
                return Result<AnonymizedAccountResult>.Failure(AnonymizeAccountErrors.AlreadyDisabled);

            // 1. Vô hiệu hóa tài khoản
            account.Disable();

            // 2. Thu hồi toàn bộ phiên đăng nhập của người dùng
            await authSessions.RevokeAllForAccountAsync(request.AccountId, now, "AccountAnonymizedByCustomer", cancellationToken);

            // 3. Xóa các tài liệu media định danh (CCCD/GPLX/Avatar)
            await media.DeleteIdentityMediaAsync(request.AccountId, cancellationToken);

            // 4. Xóa trắng thông tin định danh cá nhân trong hồ sơ
            var profile = await profiles.FindByAccountIdAsync(request.AccountId, cancellationToken);
            if (profile is not null)
            {
                profile.ClearIdentity(now);
                await profiles.SaveChangesAsync(cancellationToken);
            }

            // 5. Ghi nhật ký kiểm toán hệ thống
            auditLog.Record(
                actionType: "account.anonymized",
                actorId: request.AccountId,
                actorEmail: account.Email,
                actorRole: account.Role.ToString(),
                targetType: "account",
                targetId: account.Id.ToString(),
                reason: "Người dùng yêu cầu xóa tài khoản và ẩn danh hóa dữ liệu (US-PRIV-003)",
                ipAddress: null);

            await accounts.SaveChangesAsync(cancellationToken);

            return Result<AnonymizedAccountResult>.Success(
                new AnonymizedAccountResult("Tài khoản và thông tin cá nhân đã được xóa và ẩn danh hóa thành công theo chuẩn quyền riêng tư."));
        }, cancellationToken);
    }
}
