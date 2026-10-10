using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.AdminAccounts;

// Một phần US-ADMIN-003 làm sớm (10/10) để FE dựng màn /admin/users.
public sealed record SearchAccountsQuery(string? Search, string? Role, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<AccountPage>>;

public sealed record AccountSummary(
    Guid Id,
    string Email,
    string? FullName,
    string Role,
    string Status,
    bool IsLocked,
    DateTimeOffset? LockoutEndUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record AccountPage(IReadOnlyList<AccountSummary> Items, int Page, int PageSize, int TotalCount);

public sealed record AccountSearchFilter(string? Search, AccountRole? Role, AccountStatus? Status, int Page, int PageSize);

public interface IAccountDirectory
{
    Task<AccountPage> SearchAsync(AccountSearchFilter filter, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

public sealed class SearchAccountsQueryValidator : AbstractValidator<SearchAccountsQuery>
{
    public const int MaxPageSize = 100;

    public SearchAccountsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(254).WithMessage("Từ khoá tìm kiếm không được vượt quá 254 ký tự.");
        RuleFor(query => query.Role!)
            .IsEnumName(typeof(AccountRole), caseSensitive: false)
            .WithMessage($"role phải là một trong: {string.Join(", ", Enum.GetNames<AccountRole>())}.")
            .When(query => !string.IsNullOrWhiteSpace(query.Role));
        RuleFor(query => query.Status!)
            .IsEnumName(typeof(AccountStatus), caseSensitive: false)
            .WithMessage($"status phải là một trong: {string.Join(", ", Enum.GetNames<AccountStatus>())}.")
            .When(query => !string.IsNullOrWhiteSpace(query.Status));
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithMessage("page phải từ 1 trở lên.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize).WithMessage($"pageSize phải từ 1 đến {MaxPageSize}.");
    }
}

public sealed class SearchAccountsQueryHandler(IAccountDirectory directory, IClock clock)
    : IRequestHandler<SearchAccountsQuery, Result<AccountPage>>
{
    public async Task<Result<AccountPage>> Handle(SearchAccountsQuery request, CancellationToken cancellationToken)
    {
        var filter = new AccountSearchFilter(
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrWhiteSpace(request.Role) ? null : Enum.Parse<AccountRole>(request.Role, ignoreCase: true),
            string.IsNullOrWhiteSpace(request.Status) ? null : Enum.Parse<AccountStatus>(request.Status, ignoreCase: true),
            request.Page,
            request.PageSize);
        return Result<AccountPage>.Success(await directory.SearchAsync(filter, clock.UtcNow, cancellationToken));
    }
}

public enum AccountStatusChange
{
    Suspend,
    Activate
}

public sealed record ChangeAccountStatusCommand(Guid AdminId, Guid AccountId, AccountStatusChange Change)
    : IRequest<Result<ChangedAccountStatus>>;

public sealed record ChangedAccountStatus(Guid AccountId, string Status, string Message);

public static class ChangeAccountStatusErrors
{
    public static readonly Error CannotChangeOwnStatus =
        new("identity.cannot_change_own_status", "Quản trị viên không thể tự đình chỉ hoặc kích hoạt tài khoản của chính mình.");

    public static readonly Error CannotChangeAdminStatus =
        new("identity.cannot_change_admin_status", "Không thể đình chỉ tài khoản Quản trị viên khác. Hãy đổi vai trò trước nếu thật sự cần.");

    public static readonly Error AccountNotVerified =
        new("identity.account_not_verified", "Tài khoản chưa xác thực email nên không thể kích hoạt thay người dùng.");
}

public sealed class ChangeAccountStatusCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IClock clock) : IRequestHandler<ChangeAccountStatusCommand, Result<ChangedAccountStatus>>
{
    public const string RevokedReason = "AccountSuspended";

    public async Task<Result<ChangedAccountStatus>> Handle(ChangeAccountStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.AdminId == request.AccountId)
            return Result<ChangedAccountStatus>.Failure(ChangeAccountStatusErrors.CannotChangeOwnStatus);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<ChangedAccountStatus>.Failure(UnlockAccountErrors.AccountNotFound);

            // Không cho Admin khoá lẫn nhau; muốn khoá thì phải hạ vai trò trước (thao tác đó cũng không tự làm với mình được).
            if (account.Role == AccountRole.SystemAdmin)
                return Result<ChangedAccountStatus>.Failure(ChangeAccountStatusErrors.CannotChangeAdminStatus);

            return request.Change == AccountStatusChange.Suspend
                ? await SuspendAsync(account, cancellationToken)
                : await ActivateAsync(account, cancellationToken);
        }, cancellationToken);
    }

    private async Task<Result<ChangedAccountStatus>> SuspendAsync(Account account, CancellationToken cancellationToken)
    {
        if (account.Status == AccountStatus.Suspended)
            return Result<ChangedAccountStatus>.Success(new ChangedAccountStatus(account.Id, account.Status.ToString(), "Tài khoản đã bị đình chỉ từ trước."));

        var now = clock.UtcNow;
        account.Suspend();
        // Refresh đã chặn tài khoản bị đình chỉ, nhưng thu hồi luôn để danh sách thiết bị của họ trống ngay.
        await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
        await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return Result<ChangedAccountStatus>.Success(new ChangedAccountStatus(account.Id, account.Status.ToString(),
            "Đã đình chỉ tài khoản và đăng xuất người dùng khỏi mọi thiết bị."));
    }

    private async Task<Result<ChangedAccountStatus>> ActivateAsync(Account account, CancellationToken cancellationToken)
    {
        // Kích hoạt tài khoản chờ xác thực sẽ bỏ qua bước OTP, nên không cho.
        if (account.Status == AccountStatus.PendingEmailVerification)
            return Result<ChangedAccountStatus>.Failure(ChangeAccountStatusErrors.AccountNotVerified);

        if (account.Status == AccountStatus.EmailVerified)
            return Result<ChangedAccountStatus>.Success(new ChangedAccountStatus(account.Id, account.Status.ToString(), "Tài khoản đang hoạt động, không có gì thay đổi."));

        account.Activate();
        await accounts.SaveChangesAsync(cancellationToken);
        return Result<ChangedAccountStatus>.Success(new ChangedAccountStatus(account.Id, account.Status.ToString(),
            "Đã kích hoạt lại tài khoản. Người dùng có thể đăng nhập."));
    }
}
