using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;

public sealed record ChangeAccountRoleCommand(Guid AdminId, Guid AccountId, string Role) : IRequest<Result<ChangedAccountRole>>;

public sealed record ChangedAccountRole(Guid AccountId, string Role, string Message);

public static class ChangeAccountRoleErrors
{
    public static readonly Error CannotChangeOwnRole =
        new("identity.cannot_change_own_role", "Quản trị viên không thể tự thay đổi vai trò của chính mình.");
}

public sealed class ChangeAccountRoleCommandValidator : AbstractValidator<ChangeAccountRoleCommand>
{
    public ChangeAccountRoleCommandValidator()
    {
        RuleFor(command => command.Role)
            .NotEmpty().WithMessage("Vai trò là bắt buộc, không được để trống.")
            .IsEnumName(typeof(AccountRole), caseSensitive: false)
            .WithMessage($"Vai trò phải là một trong: {string.Join(", ", Enum.GetNames<AccountRole>())}.");
    }
}

// Quyết định 09/10: Admin không tự đổi vai trò của mình; đổi xong thu hồi mọi phiên để token mới mang vai trò mới.
public sealed class ChangeAccountRoleCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IClock clock) : IRequestHandler<ChangeAccountRoleCommand, Result<ChangedAccountRole>>
{
    public const string RevokedReason = "RoleChanged";

    public async Task<Result<ChangedAccountRole>> Handle(ChangeAccountRoleCommand request, CancellationToken cancellationToken)
    {
        // Chặn trước cả khi tìm tài khoản: tránh trường hợp Admin cuối cùng tự hạ quyền, hệ thống mất Admin.
        if (request.AdminId == request.AccountId)
            return Result<ChangedAccountRole>.Failure(ChangeAccountRoleErrors.CannotChangeOwnRole);

        var role = Enum.Parse<AccountRole>(request.Role, ignoreCase: true);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<ChangedAccountRole>.Failure(UnlockAccountErrors.AccountNotFound);

            // Gọi lại với cùng vai trò thì không làm gì, không đăng xuất người dùng vô cớ.
            if (account.Role == role)
                return Result<ChangedAccountRole>.Success(new ChangedAccountRole(account.Id, role.ToString(), "Tài khoản đã có vai trò này, không có gì thay đổi."));

            var now = clock.UtcNow;
            account.AssignRole(role);
            await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<ChangedAccountRole>.Success(new ChangedAccountRole(account.Id, role.ToString(),
                "Đã đổi vai trò. Người dùng được đăng xuất khỏi mọi thiết bị và cần đăng nhập lại để nhận quyền mới."));
        }, cancellationToken);
    }
}
