using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Accounts.GoogleLogin;

public sealed record GoogleLoginCommand(string IdToken, string? DeviceInfo = null, string? IpAddress = null) : IRequest<Result<LoginResponse>>;

public static class GoogleLoginErrors
{
    public static readonly Error InvalidGoogleToken =
        new("identity.invalid_google_token", "Không thể xác thực mã đăng nhập Google. Vui lòng thử lại.");

    public static readonly Error GoogleLoginUnavailable =
        new("identity.google_login_unavailable", "Đăng nhập bằng Google tạm thời không khả dụng. Vui lòng đăng nhập bằng email và mật khẩu.");
}

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.IdToken)
            .NotEmpty().WithMessage("Mã xác thực Google (IdToken) là bắt buộc, không được để trống.")
            .MaximumLength(4096).WithMessage("Mã xác thực Google (IdToken) không được vượt quá 4096 ký tự.");
    }
}

// US-AUTH-002 (G1–G4, Context point 19).
public sealed class GoogleLoginCommandHandler(
    IGoogleIdTokenValidator googleTokens,
    IAccountRepository accounts,
    IGoogleAccountRepository googleAccounts,
    IProfileRepository profiles,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator refreshTokenGenerator,
    IJwtTokenIssuer jwtTokenIssuer,
    IClock clock) : IRequestHandler<GoogleLoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        if (!googleTokens.IsConfigured)
            return Result<LoginResponse>.Failure(GoogleLoginErrors.GoogleLoginUnavailable);

        var google = await googleTokens.ValidateAsync(request.IdToken, cancellationToken);
        if (google is null)
            return Result<LoginResponse>.Failure(GoogleLoginErrors.InvalidGoogleToken);

        var email = Account.NormalizeEmail(google.Email);
        var now = clock.UtcNow;

        // First sign-in: create the account outside the lock. A concurrent first sign-in loses on the unique
        // email index and simply continues with the account the other request created.
        var known = await googleAccounts.FindByGoogleSubjectAsync(google.Subject, cancellationToken)
                    ?? await accounts.FindByEmailAsync(email, cancellationToken);
        if (known is null)
        {
            var created = Account.RegisterWithGoogle(Guid.NewGuid(), email, google.Subject, now);
            accounts.Add(created);
            var name = google.Name?.Trim();
            if (!string.IsNullOrEmpty(name))
                profiles.Add(Profile.Create(created.Id, name.Length > Profile.MaxFullNameLength ? name[..Profile.MaxFullNameLength] : name, now));
            await accounts.SaveNewAccountAsync(cancellationToken);
        }

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await googleAccounts.FindByGoogleSubjectForUpdateAsync(google.Subject, cancellationToken)
                          ?? await accounts.FindByEmailForUpdateAsync(email, cancellationToken);

            // The email belongs to an account already linked to a different Google account.
            if (account is null || (account.GoogleSubject is not null && account.GoogleSubject != google.Subject))
                return Result<LoginResponse>.Failure(GoogleLoginErrors.InvalidGoogleToken);

            if (account.Status == AccountStatus.Suspended)
                return Result<LoginResponse>.Failure(LoginErrors.AccountSuspended);

            if (account.Status == AccountStatus.Disabled)
                return Result<LoginResponse>.Failure(LoginErrors.AccountDisabled);

            // Context 6.1: three wrong passwords lock the login for 15 minutes, whatever the sign-in method.
            if (account.IsLockedOut(now))
                return Result<LoginResponse>.Failure(AccountMessages.Locked(account, now));

            if (account.GoogleSubject is null || account.Status == AccountStatus.PendingEmailVerification)
                account.LinkGoogle(google.Subject);

            var response = await LoginSession.StartAsync(
                account, request.DeviceInfo, request.IpAddress, now,
                accounts, authSessions, refreshTokens, refreshTokenGenerator, jwtTokenIssuer, cancellationToken);

            return Result<LoginResponse>.Success(response);
        }, cancellationToken);
    }
}
