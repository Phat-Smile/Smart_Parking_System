using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.Login;

public sealed record LoginCommand(string Email, string Password, string? DeviceInfo = null, string? IpAddress = null) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    AccountDto Account);

public sealed record AccountDto(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string Status,
    string Role);

public static class LoginErrors
{
    public static readonly Error InvalidCredentials =
        new("identity.invalid_credentials", "Email hoặc mật khẩu không chính xác.");

    public static readonly Error EmailNotVerified =
        new("identity.email_not_verified", "Email chưa được xác thực. Vui lòng nhập mã OTP đã gửi đến email, hoặc chọn \"Gửi lại mã\" nếu không nhận được.");

    // The account was created with Google and has never set a password.
    public static readonly Error PasswordNotSet =
        new("identity.password_not_set", "Tài khoản này đăng ký bằng Google và chưa có mật khẩu. Vui lòng chọn \"Tiếp tục với Google\", hoặc dùng \"Quên mật khẩu\" để tạo mật khẩu.");

    public static readonly Error AccountTemporarilyLocked =
        new("identity.account_temporarily_locked", "Tài khoản đang tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau, hoặc dùng \"Quên mật khẩu\" để mở khóa ngay.");

    public static readonly Error AccountSuspended =
        new("identity.account_suspended", "Tài khoản đã bị đình chỉ hoạt động. " + AccountMessages.ContactAdmin);

    public static readonly Error AccountDisabled =
        new("identity.account_disabled", "Tài khoản đã bị vô hiệu hóa. " + AccountMessages.ContactAdmin);
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email là bắt buộc, không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng hợp lệ.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc, không được để trống.")
            .Must(PasswordPolicy.FitsBcrypt).WithMessage(PasswordPolicy.TooLongMessage);
    }
}

public sealed class LoginCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer jwtTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);

            if (account is null)
            {
                passwordHasher.VerifyDummy(request.Password);
                return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
            }

            if (account.IsLockedOut(now))
            {
                return Result<LoginResponse>.Failure(AccountMessages.Locked(account, now));
            }

            account.ClearExpiredLockout(now);

            // G3 (changed 2026-10-09): an account created with Google has no password yet. The attempt still counts
            // towards the lockout, but the user is told to sign in with Google (identity.password_not_set).
            bool passwordMatched;
            if (account.PasswordHash is { } passwordHash)
            {
                passwordMatched = passwordHasher.Verify(request.Password, passwordHash);
            }
            else
            {
                passwordHasher.VerifyDummy(request.Password);
                passwordMatched = false;
            }
            if (!passwordMatched)
            {
                account.RecordFailedLogin(now);
                await accounts.SaveChangesAsync(cancellationToken);

                if (account.IsLockedOut(now))
                    return Result<LoginResponse>.Failure(AccountMessages.Locked(account, now));

                return Result<LoginResponse>.Failure(account.HasPassword
                    ? AccountMessages.InvalidCredentials(account)
                    : LoginErrors.PasswordNotSet);
            }

            // Correct password breaks consecutive failed attempts streak
            account.RecordCorrectCredential();

            if (account.Status == AccountStatus.Suspended)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.AccountSuspended);
            }

            if (account.Status == AccountStatus.Disabled)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.AccountDisabled);
            }

            if (account.Status != AccountStatus.EmailVerified)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.EmailNotVerified);
            }

            var response = await LoginSession.StartAsync(
                account, request.DeviceInfo, request.IpAddress, now,
                accounts, authSessions, refreshTokens, refreshTokenGenerator, jwtTokenIssuer, cancellationToken);

            return Result<LoginResponse>.Success(response);
        }, cancellationToken);
    }
}
