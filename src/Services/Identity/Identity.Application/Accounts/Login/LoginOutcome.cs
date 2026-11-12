namespace ParkingSystem.Identity.Application.Accounts.Login;

// Đăng nhập xong (có token) hoặc còn bước MFA (có mfaToken để gọi /login/mfa); luôn đúng một trong hai.
public sealed record LoginOutcome(LoginResponse? Session, MfaChallengeIssued? Mfa)
{
    public static LoginOutcome Completed(LoginResponse session) => new(session, null);

    public static LoginOutcome MfaRequired(MfaChallengeIssued challenge) => new(null, challenge);
}

public sealed record MfaChallengeIssued(string MfaToken, int ExpiresInSeconds);
