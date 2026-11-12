using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;

namespace ParkingSystem.Payment.Infrastructure.Gateways;

public class VnpayPaymentGateway : IPaymentGateway, IVnpayPaymentGateway
{
    private readonly VnpayOptions _options;

    public VnpayPaymentGateway(IOptions<VnpayOptions> options)
    {
        _options = options.Value;
    }

    // Tạo liên kết thanh toán VNPay có chữ ký số
    public string BuildPaymentUrl(
        string txnRef, // Mã giao dịch gửi sang VNPay
        long amountVnd, // Số tiền cần thanh toán
        string orderInfo, // Nội dung hiển thị khi thanh toán
        DateTimeOffset createdAtUtc, // Thời gian tạo giao dịch
        DateTimeOffset expiresAtUtc, // Thời gian hết hạn quét mã
        string clientIp) // Địa chỉ IP người dùng
    {
        var vnpayParams = new SortedList<string, string>(
            Comparer<string>.Create((x, y) => string.Compare(x, y, CultureInfo.InvariantCulture, CompareOptions.Ordinal)))
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = (amountVnd * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = createdAtUtc.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = NormalizeIp(clientIp),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = expiresAtUtc.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };

        var queryString = new StringBuilder();
        var rawData = new StringBuilder();

        foreach (var (key, value) in vnpayParams)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (queryString.Length > 0)
            {
                queryString.Append('&');
                rawData.Append('&');
            }

            var encodedKey = WebUtility.UrlEncode(key);
            var encodedValue = WebUtility.UrlEncode(value);

            queryString.Append(encodedKey).Append('=').Append(encodedValue);
            rawData.Append(encodedKey).Append('=').Append(encodedValue);
        }

        var secureHash = ComputeHmacSha512(_options.HashSecret, rawData.ToString());
        return $"{_options.BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public string CreatePaymentUrl(
        string txnRef,
        decimal amount,
        DateTimeOffset expiresAtUtc,
        string clientIp)
    {
        var vnpayParams = new SortedList<string, string>(
            Comparer<string>.Create((x, y) => string.Compare(x, y, CultureInfo.InvariantCulture, CompareOptions.Ordinal)))
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = ((long)Math.Round(amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = NormalizeIp(clientIp),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Nap tien vi {txnRef}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = expiresAtUtc.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };

        var queryString = new StringBuilder();
        var rawData = new StringBuilder();

        foreach (var (key, value) in vnpayParams)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (queryString.Length > 0)
            {
                queryString.Append('&');
                rawData.Append('&');
            }

            var encodedKey = WebUtility.UrlEncode(key);
            var encodedValue = WebUtility.UrlEncode(value);

            queryString.Append(encodedKey).Append('=').Append(encodedValue);
            rawData.Append(encodedKey).Append('=').Append(encodedValue);
        }

        var secureHash = ComputeHmacSha512(_options.HashSecret, rawData.ToString());
        return $"{_options.BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters)
    {
        if (queryParameters is null ||
            string.IsNullOrEmpty(_options.HashSecret) ||
            string.IsNullOrEmpty(_options.TmnCode))
        {
            return false;
        }

        if (!queryParameters.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
        {
            return false;
        }

        // Kiểm tra đúng mã điểm bán hàng đã đăng ký
        if (!queryParameters.TryGetValue("vnp_TmnCode", out var tmnCode) ||
            !string.Equals(tmnCode, _options.TmnCode, StringComparison.Ordinal))
        {
            return false;
        }

        var vnpayParams = new SortedList<string, string>(
            Comparer<string>.Create((x, y) => string.Compare(x, y, CultureInfo.InvariantCulture, CompareOptions.Ordinal)));

        foreach (var (key, value) in queryParameters)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            // Chỉ lấy các tham số bắt đầu bằng vnp_ và bỏ qua chữ ký hash
            if (key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                vnpayParams[key] = value;
            }
        }

        var rawData = new StringBuilder();
        foreach (var (key, value) in vnpayParams)
        {
            if (rawData.Length > 0)
            {
                rawData.Append('&');
            }

            var encodedKey = WebUtility.UrlEncode(key);
            var encodedValue = WebUtility.UrlEncode(value);
            rawData.Append(encodedKey).Append('=').Append(encodedValue);
        }

        var computedHash = ComputeHmacSha512(_options.HashSecret, rawData.ToString());

        // So sánh thời gian cố định để chống Timing Attack
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(secureHash.ToLowerInvariant()));
    }

    private static string NormalizeIp(string clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp) || clientIp == "::1")
        {
            return "127.0.0.1";
        }

        return clientIp;
    }

    private static string ComputeHmacSha512(string secret, string data)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
