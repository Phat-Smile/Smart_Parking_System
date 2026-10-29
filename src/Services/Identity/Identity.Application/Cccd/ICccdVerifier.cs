namespace ParkingSystem.Identity.Application.Cccd;

public interface ICccdVerifier
{
    Task<CccdVerificationResult> VerifyAsync(string cccd, CancellationToken cancellationToken);
}
