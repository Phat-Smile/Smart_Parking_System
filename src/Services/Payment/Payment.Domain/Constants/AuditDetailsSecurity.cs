namespace ParkingSystem.Payment.Domain.Constants;

/// <summary>
/// Kiểm tra an toàn cho trường JSON details của audit log.
/// Chặn tuyệt đối rò rỉ mật khẩu, token, OTP và bí mật vào nhật ký kiểm toán.
/// </summary>
public static class AuditDetailsSecurity
{
    // Danh sách các từ khóa cấm xuất hiện trong key của details
    private static readonly string[] ForbiddenKeyKeywords =
    [
        "password",
        "token",
        "secret",
        "otp",
        "key",
        "hash",
        "credential",
        "authorization"
    ];

    public static bool ContainsForbiddenKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        var lower = key.Trim().ToLowerInvariant();
        return ForbiddenKeyKeywords.Any(forbidden => lower.Contains(forbidden));
    }
}
