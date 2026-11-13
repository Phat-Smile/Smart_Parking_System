using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ParkingSystem.Parking.WebAPI.OwnerLocation;

// Uses ASP.NET Core's authenticated time-limited protection rather than trusting a boolean in
// the registration payload. The token binds the authenticated Owner, address, and exact pin.
// Deployments with more than one instance must share a persistent Data Protection key ring.
public sealed class OwnerLotLocationConfirmationService(IDataProtectionProvider dataProtection)
{
    public const string TokenPurpose = "Parking.OwnerLotEntranceConfirmation.v1";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    private readonly ITimeLimitedDataProtector _protector = dataProtection
        .CreateProtector(TokenPurpose).ToTimeLimitedDataProtector();

    public OwnerLotLocationConfirmation Issue(Guid ownerId, string address, decimal latitude, decimal longitude)
    {
        var expiry = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var payload = JsonSerializer.Serialize(new OwnerLotConfirmedPinPayload(
            ownerId, address.Trim(), latitude, longitude));
        var token = _protector.Protect(payload, expiry);
        return new OwnerLotLocationConfirmation(token, expiry);
    }

    public bool MatchesRegistration(
        Guid ownerId,
        string? confirmationToken,
        string? address,
        decimal? latitude,
        decimal? longitude,
        bool requireConfirmation)
    {
        if (string.IsNullOrWhiteSpace(confirmationToken))
            return !requireConfirmation;

        if (confirmationToken.Length > 8192 || ownerId == Guid.Empty
            || string.IsNullOrWhiteSpace(address) || !latitude.HasValue || !longitude.HasValue)
            return false;

        try
        {
            var json = _protector.Unprotect(confirmationToken);
            var confirmed = JsonSerializer.Deserialize<OwnerLotConfirmedPinPayload>(json);
            return confirmed is not null
                && confirmed.OwnerId == ownerId
                && string.Equals(confirmed.Address, address.Trim(), StringComparison.Ordinal)
                && confirmed.Latitude == latitude.Value
                && confirmed.Longitude == longitude.Value;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException or FormatException)
        {
            // Corrupt, forged, expired, or malformed tokens always fail closed.
            return false;
        }
    }

}

internal sealed record OwnerLotConfirmedPinPayload(Guid OwnerId, string Address, decimal Latitude, decimal Longitude);

public sealed record OwnerLotLocationConfirmation(string LocationConfirmationToken, DateTimeOffset ExpiresAtUtc);
