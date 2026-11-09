using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Mfa;

namespace ParkingSystem.Identity.Application.Accounts.Mfa;

public static class MfaErrors
{
    public static readonly Error AlreadyEnabled =
        new("identity.mfa_already_enabled", "Xác thực hai lớp đã được bật cho tài khoản này.");

    public static readonly Error SetupRequired =
        new("identity.mfa_setup_required", "Chưa có mã cài đặt xác thực hai lớp. Vui lòng bấm \"Bật xác thực hai lớp\" để tạo mã QR trước.");

    public static readonly Error NotEnabled =
        new("identity.mfa_not_enabled", "Tài khoản chưa bật xác thực hai lớp.");

    public static readonly Error RequiredForAdmin =
        new("identity.mfa_required_for_admin", "Tài khoản Quản trị viên bắt buộc dùng xác thực hai lớp nên không thể tắt.");

    public static readonly Error ChallengeInvalid =
        new("identity.mfa_challenge_invalid", "Phiên xác thực hai lớp đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.");

    public static readonly Error InvalidCode =
        new("identity.invalid_mfa_code", "Mã xác thực không đúng.");

    public static Error InvalidCodeFor(Account account) => InvalidCode with
    {
        Message = $"Mã xác thực không đúng. Bạn còn {Math.Max(0, Account.MaxFailedAttemptsBeforeLockout - account.FailedLoginAttempts)} lần thử trước khi tài khoản bị tạm khóa {Account.LockoutDurationMinutes} phút."
    };
}

// Token ngẫu nhiên và recovery code: client giữ bản gốc, DB chỉ giữ SHA-256.
public static class MfaSecrets
{
    // Bỏ các ký tự dễ nhầm (0/O, 1/I/L) vì người dùng phải chép tay recovery code.
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static (string Raw, string Hash) NewChallengeToken()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (raw, Hash(raw));
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();

    public static string NewRecoveryCode()
    {
        var chars = Enumerable.Range(0, 8).Select(_ => RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)]).ToArray();
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
    }

    // Người dùng có thể gõ thường, thiếu gạch nối hoặc thừa khoảng trắng.
    public static string HashRecoveryCode(string code) =>
        Hash(code.Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant());

    public static IReadOnlyList<string> IssueRecoveryCodes(Guid accountId, DateTimeOffset nowUtc, IMfaRepository mfa)
    {
        var raw = Enumerable.Range(0, MfaRecoveryCode.CodesPerSet).Select(_ => NewRecoveryCode()).ToList();
        mfa.AddRecoveryCodes(raw.Select(code => MfaRecoveryCode.Create(accountId, HashRecoveryCode(code), nowUtc)));
        return raw;
    }
}

// Đăng nhập (mật khẩu hoặc Google) gọi trước khi mở phiên: tài khoản đã bật MFA thì dừng ở bước 2.
public sealed class MfaLoginGate(IMfaRepository mfa)
{
    public Task<bool> IsRequiredAsync(Guid accountId, CancellationToken cancellationToken) => mfa.IsEnabledAsync(accountId, cancellationToken);

    public MfaChallengeIssued IssueChallenge(Account account, string authenticationMethod, string? deviceInfo, string? ipAddress, DateTimeOffset nowUtc)
    {
        var (raw, hash) = MfaSecrets.NewChallengeToken();
        mfa.AddChallenge(MfaChallenge.Issue(account.Id, hash, authenticationMethod, deviceInfo, ipAddress, nowUtc));
        return new MfaChallengeIssued(raw, MfaChallenge.LifetimeMinutes * 60);
    }
}

internal static class MfaCodeRules
{
    public static IRuleBuilderOptions<T, string> IsTotpCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Mã xác thực là bắt buộc, không được để trống.")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác thực phải gồm đúng 6 chữ số.");
}

// ---------------- Bật / tắt ----------------

public sealed record SetupMfaCommand(Guid AccountId) : IRequest<Result<MfaSetup>>;

public sealed record MfaSetup(string Secret, string OtpauthUri, string Message);

public sealed class SetupMfaCommandHandler(
    IAccountRepository accounts, IMfaRepository mfa, ITotpService totp, IMfaSecretProtector protector, IClock clock)
    : IRequestHandler<SetupMfaCommand, Result<MfaSetup>>
{
    public async Task<Result<MfaSetup>> Handle(SetupMfaCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<MfaSetup>.Failure(UnlockAccountErrors.AccountNotFound);

            var existing = await mfa.FindAsync(account.Id, cancellationToken);
            if (existing is { IsEnabled: true })
                return Result<MfaSetup>.Failure(MfaErrors.AlreadyEnabled);

            var now = clock.UtcNow;
            var secret = totp.GenerateSecret();
            if (existing is null)
                mfa.Add(AccountMfa.StartSetup(account.Id, protector.Protect(secret), now));
            else
                existing.RestartSetup(protector.Protect(secret), now);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<MfaSetup>.Success(new MfaSetup(secret, totp.BuildProvisioningUri(account.Email, secret),
                "Quét mã QR bằng Google Authenticator rồi nhập mã 6 số để hoàn tất."));
        }, cancellationToken);
    }
}

public sealed record ConfirmMfaCommand(Guid AccountId, string Code) : IRequest<Result<MfaEnabled>>;

public sealed record MfaEnabled(IReadOnlyList<string> RecoveryCodes, string Message);

public sealed class ConfirmMfaCommandValidator : AbstractValidator<ConfirmMfaCommand>
{
    public ConfirmMfaCommandValidator() => RuleFor(command => command.Code).IsTotpCode();
}

public sealed class ConfirmMfaCommandHandler(
    IAccountRepository accounts, IMfaRepository mfa, IAuthSessionRepository authSessions, IRefreshTokenRepository refreshTokens,
    ITotpService totp, IMfaSecretProtector protector, IClock clock)
    : IRequestHandler<ConfirmMfaCommand, Result<MfaEnabled>>
{
    public const string RevokedReason = "MfaEnabled";

    public async Task<Result<MfaEnabled>> Handle(ConfirmMfaCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<MfaEnabled>.Failure(UnlockAccountErrors.AccountNotFound);

            var now = clock.UtcNow;
            if (account.IsLockedOut(now))
                return Result<MfaEnabled>.Failure(AccountMessages.Locked(account, now));

            var setup = await mfa.FindAsync(account.Id, cancellationToken);
            if (setup is null)
                return Result<MfaEnabled>.Failure(MfaErrors.SetupRequired);
            if (setup.IsEnabled)
                return Result<MfaEnabled>.Failure(MfaErrors.AlreadyEnabled);

            var step = totp.FindMatchingStep(protector.Unprotect(setup.SecretProtected), request.Code, now);
            if (step is null)
                return await MfaFailures.RecordAsync<MfaEnabled>(account, accounts, now, cancellationToken);

            setup.Enable(step.Value, now);
            account.RecordCorrectCredential();
            await mfa.DeleteRecoveryCodesAsync(account.Id, cancellationToken);
            var codes = MfaSecrets.IssueRecoveryCodes(account.Id, now, mfa);
            // Đăng xuất mọi phiên: lần đăng nhập sau mới qua bước 2 và token có amr "mfa" (Admin cần để dùng API quản trị).
            await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<MfaEnabled>.Success(new MfaEnabled(codes,
                "Đã bật xác thực hai lớp. Hãy lưu 10 mã khôi phục ở nơi an toàn (mỗi mã dùng một lần) rồi đăng nhập lại."));
        }, cancellationToken);
    }
}

public sealed record DisableMfaCommand(Guid AccountId, string? Password, string Code) : IRequest<Result<MfaDisabled>>;

public sealed record MfaDisabled(string Message);

public sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator()
    {
        RuleFor(command => command.Code).IsTotpCode();
        RuleFor(command => command.Password).MaximumLength(256);
    }
}

public sealed class DisableMfaCommandHandler(
    IAccountRepository accounts, IMfaRepository mfa, IPasswordHasher passwordHasher, ITotpService totp, IMfaSecretProtector protector, IClock clock)
    : IRequestHandler<DisableMfaCommand, Result<MfaDisabled>>
{
    public async Task<Result<MfaDisabled>> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<MfaDisabled>.Failure(UnlockAccountErrors.AccountNotFound);

            // Quyết định 09/10: MFA bắt buộc với Admin.
            if (account.Role == AccountRole.SystemAdmin)
                return Result<MfaDisabled>.Failure(MfaErrors.RequiredForAdmin);

            var now = clock.UtcNow;
            if (account.IsLockedOut(now))
                return Result<MfaDisabled>.Failure(AccountMessages.Locked(account, now));

            var current = await mfa.FindAsync(account.Id, cancellationToken);
            if (current is not { IsEnabled: true })
                return Result<MfaDisabled>.Failure(MfaErrors.NotEnabled);

            // Tài khoản chỉ dùng Google không có mật khẩu, khi đó mã TOTP là bằng chứng duy nhất.
            if (account.PasswordHash is { } hash && !passwordHasher.Verify(request.Password ?? string.Empty, hash))
            {
                account.RecordFailedLogin(now);
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<MfaDisabled>.Failure(account.IsLockedOut(now) ? AccountMessages.Locked(account, now) : ChangePasswordErrors.InvalidCurrentPassword);
            }

            var step = totp.FindMatchingStep(protector.Unprotect(current.SecretProtected), request.Code, now);
            if (step is null || !current.TryUseStep(step.Value))
                return await MfaFailures.RecordAsync<MfaDisabled>(account, accounts, now, cancellationToken);

            mfa.Remove(current);
            await mfa.DeleteRecoveryCodesAsync(account.Id, cancellationToken);
            account.RecordCorrectCredential();
            await accounts.SaveChangesAsync(cancellationToken);
            return Result<MfaDisabled>.Success(new MfaDisabled("Đã tắt xác thực hai lớp."));
        }, cancellationToken);
    }
}

public sealed record RegenerateRecoveryCodesCommand(Guid AccountId, string Code) : IRequest<Result<MfaEnabled>>;

public sealed class RegenerateRecoveryCodesCommandValidator : AbstractValidator<RegenerateRecoveryCodesCommand>
{
    public RegenerateRecoveryCodesCommandValidator() => RuleFor(command => command.Code).IsTotpCode();
}

public sealed class RegenerateRecoveryCodesCommandHandler(
    IAccountRepository accounts, IMfaRepository mfa, ITotpService totp, IMfaSecretProtector protector, IClock clock)
    : IRequestHandler<RegenerateRecoveryCodesCommand, Result<MfaEnabled>>
{
    public async Task<Result<MfaEnabled>> Handle(RegenerateRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<MfaEnabled>.Failure(UnlockAccountErrors.AccountNotFound);

            var now = clock.UtcNow;
            if (account.IsLockedOut(now))
                return Result<MfaEnabled>.Failure(AccountMessages.Locked(account, now));

            var current = await mfa.FindAsync(account.Id, cancellationToken);
            if (current is not { IsEnabled: true })
                return Result<MfaEnabled>.Failure(MfaErrors.NotEnabled);

            var step = totp.FindMatchingStep(protector.Unprotect(current.SecretProtected), request.Code, now);
            if (step is null || !current.TryUseStep(step.Value))
                return await MfaFailures.RecordAsync<MfaEnabled>(account, accounts, now, cancellationToken);

            await mfa.DeleteRecoveryCodesAsync(account.Id, cancellationToken);
            var codes = MfaSecrets.IssueRecoveryCodes(account.Id, now, mfa);
            account.RecordCorrectCredential();
            await accounts.SaveChangesAsync(cancellationToken);
            return Result<MfaEnabled>.Success(new MfaEnabled(codes, "Đã tạo bộ mã khôi phục mới; các mã cũ không còn dùng được."));
        }, cancellationToken);
    }
}

// ---------------- Bước 2 của đăng nhập ----------------

public sealed record CompleteMfaLoginCommand(string MfaToken, string? Code, string? RecoveryCode) : IRequest<Result<LoginResponse>>;

public sealed class CompleteMfaLoginCommandValidator : AbstractValidator<CompleteMfaLoginCommand>
{
    public CompleteMfaLoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(command => command.MfaToken).NotEmpty().WithMessage("mfaToken là bắt buộc.").MaximumLength(128);
        RuleFor(command => command)
            .Must(command => string.IsNullOrWhiteSpace(command.Code) != string.IsNullOrWhiteSpace(command.RecoveryCode))
            .WithName("code")
            .WithMessage("Nhập mã 6 số từ ứng dụng xác thực hoặc một mã khôi phục (chỉ một trong hai).");
        RuleFor(command => command.Code!).Matches("^[0-9]{6}$").WithMessage("Mã xác thực phải gồm đúng 6 chữ số.")
            .When(command => !string.IsNullOrWhiteSpace(command.Code));
        RuleFor(command => command.RecoveryCode!).Matches("^[A-Za-z0-9]{4}-?[A-Za-z0-9]{4}$").WithMessage("Mã khôi phục có dạng XXXX-XXXX.")
            .When(command => !string.IsNullOrWhiteSpace(command.RecoveryCode));
    }
}

public sealed class CompleteMfaLoginCommandHandler(
    IAccountRepository accounts,
    IMfaRepository mfa,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator refreshTokenGenerator,
    IJwtTokenIssuer jwtTokenIssuer,
    ITotpService totp,
    IMfaSecretProtector protector,
    IClock clock) : IRequestHandler<CompleteMfaLoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(CompleteMfaLoginCommand request, CancellationToken cancellationToken)
    {
        var challenge = await mfa.FindChallengeByTokenHashAsync(MfaSecrets.Hash(request.MfaToken), cancellationToken);
        if (challenge is null)
            return Result<LoginResponse>.Failure(MfaErrors.ChallengeInvalid);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Khoá account trước rồi đọc lại challenge: hai request cùng mfaToken chạy lần lượt, chỉ một cái mở được phiên.
            var account = await accounts.FindByIdForUpdateAsync(challenge.AccountId, cancellationToken);
            await mfa.ReloadAsync(challenge, cancellationToken);
            var now = clock.UtcNow;
            if (account is null || !challenge.IsUsable(now))
                return Result<LoginResponse>.Failure(MfaErrors.ChallengeInvalid);

            if (account.IsLockedOut(now))
                return Result<LoginResponse>.Failure(AccountMessages.Locked(account, now));
            if (account.Status == AccountStatus.Suspended)
                return Result<LoginResponse>.Failure(LoginErrors.AccountSuspended);
            if (account.Status == AccountStatus.Disabled)
                return Result<LoginResponse>.Failure(LoginErrors.AccountDisabled);

            var current = await mfa.FindAsync(account.Id, cancellationToken);
            if (current is not { IsEnabled: true })
                return Result<LoginResponse>.Failure(MfaErrors.ChallengeInvalid);

            if (!await VerifyAsync(request, current, account.Id, now, cancellationToken))
            {
                challenge.RecordFailedAttempt();
                return await MfaFailures.RecordAsync<LoginResponse>(account, accounts, now, cancellationToken);
            }

            challenge.Complete(now);
            account.RecordCorrectCredential();
            var response = await LoginSession.StartAsync(
                account, challenge.DeviceInfo, challenge.IpAddress, now,
                accounts, authSessions, refreshTokens, refreshTokenGenerator, jwtTokenIssuer, cancellationToken,
                $"{challenge.AuthenticationMethod} mfa");
            return Result<LoginResponse>.Success(response);
        }, cancellationToken);
    }

    private async Task<bool> VerifyAsync(CompleteMfaLoginCommand request, AccountMfa current, Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var step = totp.FindMatchingStep(protector.Unprotect(current.SecretProtected), request.Code, now);
            return step is not null && current.TryUseStep(step.Value);
        }

        var hash = MfaSecrets.HashRecoveryCode(request.RecoveryCode!);
        var code = (await mfa.ListUnusedRecoveryCodesAsync(accountId, cancellationToken)).FirstOrDefault(c => c.CodeHash == hash);
        code?.Use(now);
        return code is not null;
    }
}

internal static class MfaFailures
{
    // Mã sai tính chung bộ đếm với mật khẩu sai: 3 lần thì khoá 15 phút, nên không đoán mã vô hạn được.
    public static async Task<Result<T>> RecordAsync<T>(Account account, IAccountRepository accounts, DateTimeOffset now, CancellationToken cancellationToken)
    {
        account.RecordFailedLogin(now);
        await accounts.SaveChangesAsync(cancellationToken);
        return Result<T>.Failure(account.IsLockedOut(now) ? AccountMessages.Locked(account, now) : MfaErrors.InvalidCodeFor(account));
    }
}
