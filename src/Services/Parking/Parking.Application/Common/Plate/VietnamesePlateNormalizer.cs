using System.Text.RegularExpressions;

namespace ParkingSystem.Parking.Application.Common.Plate;

/// <summary>
/// Chuẩn hóa và nhận diện định dạng biển số xe cơ giới Việt Nam theo quy chuẩn Bộ GTVT.
/// </summary>
public static partial class VietnamesePlateNormalizer
{
    private static readonly Regex PlateCleanRegex = new(@"[^A-Z0-9]", RegexOptions.Compiled);
    private static readonly Regex GeneralPlateRegex = new(@"^[0-9]{2}[A-Z0-9]{1,3}[0-9]{4,5}$", RegexOptions.Compiled);

    // Motorbike series usually has digit after letter: e.g. 59-X1, 29-B2, 43-D1, or 50-Y9 followed by 5 digits (total 9 chars)
    private static readonly Regex ModernMotorbikeRegex = new(@"^[0-9]{2}[A-Z][0-9][0-9]{5}$", RegexOptions.Compiled);

    /// <summary>
    /// Xóa toàn bộ ký tự đặc biệt (khoảng trắng, dấu chấm, dấu gạch nối) và chuyển thành chữ in hoa.
    /// Ví dụ: "51f - 782.91" -> "51F78291"
    /// </summary>
    public static string Normalize(string? rawPlate)
    {
        if (string.IsNullOrWhiteSpace(rawPlate))
        {
            return string.Empty;
        }

        var upper = rawPlate.Trim().ToUpperInvariant();
        return PlateCleanRegex.Replace(upper, string.Empty);
    }

    /// <summary>
    /// Kiểm tra biển số có hợp lệ theo định dạng biển số xe Việt Nam không.
    /// </summary>
    public static bool IsValidFormat(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return false;
        }

        return GeneralPlateRegex.IsMatch(normalizedPlate);
    }

    /// <summary>
    /// Định dạng biển số hiển thị trực quan đẹp mắt (VD: 51F-782.91 hoặc 59X1-234.56).
    /// </summary>
    public static string FormatDisplay(string normalizedPlate, string? vehicleType = null)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate) || normalizedPlate.Length < 7)
        {
            return normalizedPlate;
        }

        var isMotorbike = string.Equals(vehicleType, "Motorbike", StringComparison.OrdinalIgnoreCase)
            || DetectVehicleType(normalizedPlate) == "Motorbike";

        if (isMotorbike && normalizedPlate.Length >= 8)
        {
            // E.g. 59X112345 -> 59X1-123.45 (or 59X1-1234)
            var prefix = normalizedPlate[..4];
            var remainder = normalizedPlate[4..];
            if (remainder.Length == 5)
            {
                return $"{prefix}-{remainder[..3]}.{remainder[3..]}";
            }
            return $"{prefix}-{remainder}";
        }

        // Standard car: 2 numbers + 1 or 2 letters (e.g. 51F, 30H, 51LD) + numbers
        var match = Regex.Match(normalizedPlate, @"^([0-9]{2}[A-Z]{1,2})([0-9]+)$");
        if (match.Success)
        {
            var series = match.Groups[1].Value;
            var digits = match.Groups[2].Value;
            if (digits.Length == 5)
            {
                return $"{series}-{digits[..3]}.{digits[3..]}";
            }
            if (digits.Length == 4)
            {
                return $"{series}-{digits}";
            }
        }

        return normalizedPlate;
    }

    /// <summary>
    /// Suy đoán loại xe (Car hoặc Motorbike) dựa trên ký hiệu sê-ri biển số.
    /// </summary>
    public static string DetectVehicleType(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate) || normalizedPlate.Length < 7)
        {
            return "Car";
        }

        if (ModernMotorbikeRegex.IsMatch(normalizedPlate))
        {
            return "Motorbike";
        }

        return "Car";
    }
}
