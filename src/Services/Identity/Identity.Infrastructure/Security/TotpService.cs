using System.Security.Cryptography;
using System.Text;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

// RFC 6238 / RFC 4226 với tham số mặc định của Google Authenticator: HMAC-SHA1, 6 số, bước 30 giây.
public sealed class TotpService : ITotpService
{
    public const int Digits = 6;
    public const int StepSeconds = 30;
    private const int SecretBytes = 20;
    private const string Issuer = "SmartParking";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    public string BuildProvisioningUri(string email, string secret)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{email}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Issuer}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public long? FindMatchingStep(string secret, string code, DateTimeOffset nowUtc)
    {
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
            return null;

        var key = Base32Decode(secret);
        var current = nowUtc.ToUnixTimeSeconds() / StepSeconds;
        var expected = Encoding.ASCII.GetBytes(code);
        // Chấp nhận lệch một bước (±30 s) vì đồng hồ điện thoại và máy chủ không khớp tuyệt đối.
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = current + offset;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ComputeCode(key, step)), expected))
                return step;
        }

        return null;
    }

    public static string ComputeCode(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);

        // Dynamic truncation (RFC 4226 mục 5.3).
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            output.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    public static byte[] Base32Decode(string text)
    {
        var clean = text.Trim().TrimEnd('=').Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Base32Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (value < 0)
                throw new FormatException("Secret is not valid Base32.");

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
