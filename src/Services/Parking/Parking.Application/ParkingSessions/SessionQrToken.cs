using System.Buffers.Text;
using System.Security.Cryptography;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions;

// BR-11.b: random token with no account id or plate inside, from a cryptographic generator.
// Used when a session opens and when staff reissue a lost QR (US-STAFF-004).
public static class SessionQrToken
{
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ParkingSession.QrTokenByteLength));
}
