namespace ParkingSystem.Identity.Application.Common.Security;

public sealed record GoogleIdentity(string Subject, string Email, string? Name);

public interface IGoogleIdTokenValidator
{
    // False when no Google client ID is configured: Google sign-in is switched off.
    bool IsConfigured { get; }

    // Null unless the token is signed by Google, issued for our client ID, unexpired and has a verified email.
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
