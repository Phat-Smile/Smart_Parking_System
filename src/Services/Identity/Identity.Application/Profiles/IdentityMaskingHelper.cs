namespace ParkingSystem.Identity.Application.Profiles;

/// <summary>
/// Hỗ trợ che (masking) thông tin định danh cá nhân nhạy cảm (CCCD/GPLX) theo US-PRIV-002.
/// Đảm bảo API không bao giờ trả về số định danh đầy đủ ra ngoài.
/// </summary>
public static class IdentityMaskingHelper
{
    /// <summary>
    /// Che số CCCD/CMND: chỉ giữ lại 4 ký tự cuối, các ký tự trước thay bằng dấu '*'.
    /// Ví dụ: "079123456789" -> "********6789".
    /// </summary>
    public static string? MaskCitizenId(string? value) => MaskLastFour(value);

    /// <summary>
    /// Che số bằng lái xe (GPLX): chỉ giữ lại 4 ký tự cuối, các ký tự trước thay bằng dấu '*'.
    /// Ví dụ: "B2-123456" -> "*****3456".
    /// </summary>
    public static string? MaskDriverLicense(string? value) => MaskLastFour(value);

    private static string? MaskLastFour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
            return new string('*', Math.Max(trimmed.Length, 4));

        var maskedPart = new string('*', trimmed.Length - 4);
        var visiblePart = trimmed[^4..];
        return $"{maskedPart}{visiblePart}";
    }
}
