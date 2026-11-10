namespace ParkingSystem.Parking.Application.Common.Plate;

/// <summary>
/// So khớp mờ biển số xe (Fuzzy License Plate Matching) dựa trên thuật toán khoảng cách Levenshtein
/// và bộ quy tắc bù trừ sai lệch nhận dạng ký tự quang học (OCR character confusions).
/// </summary>
public static class FuzzyPlateMatcher
{
    private static readonly HashSet<(char, char)> ConfusablePairs = new()
    {
        ('8', 'B'), ('B', '8'),
        ('0', 'D'), ('D', '0'),
        ('0', 'O'), ('O', '0'),
        ('0', 'Q'), ('Q', '0'),
        ('1', 'I'), ('I', '1'),
        ('1', 'L'), ('L', '1'),
        ('2', 'Z'), ('Z', '2'),
        ('5', 'S'), ('S', '5'),
        ('6', 'G'), ('G', '6'),
        ('U', 'V'), ('V', 'U'),
    };

    /// <summary>
    /// Tính khoảng cách Levenshtein cơ bản giữa 2 chuỗi biển số xe.
    /// </summary>
    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// Kiểm tra 2 biển số có tương đồng nhau không (cho phép sai khác tối đa maxAllowedDistance).
    /// Nếu sự sai khác nằm trong các cặp ký tự dễ nhầm lẫn (8-B, 0-D, 1-I,...), coi là tương đồng.
    /// </summary>
    public static bool ArePlatesSimilar(string plate1, string plate2, int maxAllowedDistance = 1)
    {
        var norm1 = VietnamesePlateNormalizer.Normalize(plate1);
        var norm2 = VietnamesePlateNormalizer.Normalize(plate2);

        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Math.Abs(norm1.Length - norm2.Length) > maxAllowedDistance)
        {
            return false;
        }

        // Trường hợp độ dài bằng nhau: kiểm tra xem số ký tự khác nhau có phải là confusable không
        if (norm1.Length == norm2.Length)
        {
            int diffCount = 0;
            bool onlyConfusableDiffs = true;

            for (int i = 0; i < norm1.Length; i++)
            {
                if (norm1[i] != norm2[i])
                {
                    diffCount++;
                    if (!ConfusablePairs.Contains((norm1[i], norm2[i])))
                    {
                        onlyConfusableDiffs = false;
                    }
                }
            }

            if (diffCount <= maxAllowedDistance)
            {
                return true;
            }

            // Nếu khác 2 ký tự nhưng cả 2 đều là cặp confusable phổ biến trong OCR biển số: vẫn chấp nhận
            if (diffCount == 2 && onlyConfusableDiffs)
            {
                return true;
            }
        }

        var distance = ComputeLevenshteinDistance(norm1, norm2);
        return distance <= maxAllowedDistance;
    }

    /// <summary>
    /// Tìm biển số khớp nhất trong danh sách các biển số ứng viên (ví dụ danh sách các phiên xe đang Active).
    /// </summary>
    public static (string? BestMatch, int Distance) FindBestMatch(string targetPlate, IEnumerable<string> candidatePlates, int maxAllowedDistance = 1)
    {
        var normTarget = VietnamesePlateNormalizer.Normalize(targetPlate);
        string? bestMatch = null;
        int minDistance = int.MaxValue;

        foreach (var candidate in candidatePlates)
        {
            var normCandidate = VietnamesePlateNormalizer.Normalize(candidate);
            if (string.Equals(normTarget, normCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return (candidate, 0);
            }

            if (ArePlatesSimilar(normTarget, normCandidate, maxAllowedDistance))
            {
                var dist = ComputeLevenshteinDistance(normTarget, normCandidate);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestMatch = candidate;
                }
            }
        }

        return minDistance <= maxAllowedDistance ? (bestMatch, minDistance) : (null, int.MaxValue);
    }
}
