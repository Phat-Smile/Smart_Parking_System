namespace ParkingSystem.Identity.Application.Common.Security;

// RFC 6238: mã 6 số đổi mỗi 30 giây, tạo từ secret dùng chung giữa server và app Authenticator.
public interface ITotpService
{
    // Secret ngẫu nhiên dạng Base32 để người dùng nhập tay hoặc quét QR.
    string GenerateSecret();

    // Chuỗi otpauth:// để FE vẽ QR cho Google Authenticator.
    string BuildProvisioningUri(string email, string secret);

    // Trả bước thời gian khớp với mã (cho lệch ±1 bước), null nếu không khớp.
    long? FindMatchingStep(string secret, string code, DateTimeOffset nowUtc);
}

// Secret TOTP lưu DB dạng mã hoá; lộ DB vẫn không sinh được mã.
public interface IMfaSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
