using ParkingSystem.Identity.Application.Cccd;

namespace ParkingSystem.Identity.Infrastructure.Cccd;

public sealed class StubCccdVerifier : ICccdVerifier
{
    private static readonly Dictionary<string, CccdVerificationResult> Registry = new()
    {
        ["000000000001"] = CccdVerificationResult.Valid("TEST USER ONE", new DateOnly(2000, 1, 1), "M"),
        ["000000000002"] = CccdVerificationResult.Valid("TEST USER TWO", new DateOnly(2001, 6, 15), "F"),
        ["000000000003"] = CccdVerificationResult.Valid("TEST ADMIN USER", new DateOnly(1995, 8, 20), "M"),
        ["000000000004"] = CccdVerificationResult.Valid("TEST STAFF USER", new DateOnly(1998, 12, 10), "F"),
    };

    public Task<CccdVerificationResult> VerifyAsync(string cccd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Registry.TryGetValue(cccd, out var match) ? match : CccdVerificationResult.Invalid);
    }
}
