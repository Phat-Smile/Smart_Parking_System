using ParkingSystem.Identity.Application.Cccd;

namespace ParkingSystem.Identity.Infrastructure.Cccd;

public sealed class StubCccdVerifier : ICccdVerifier
{
    private static readonly Dictionary<string, CccdVerificationResult> Registry = new()
    {
        ["000000000001"] = new CccdVerificationResult(true, "TEST USER ONE", new DateOnly(2000, 1, 1), "M"),
        ["000000000002"] = new CccdVerificationResult(true, "TEST USER TWO", new DateOnly(2001, 6, 15), "F"),
        ["000000000003"] = new CccdVerificationResult(true, "TEST ADMIN USER", new DateOnly(1995, 8, 20), "M"),
        ["000000000004"] = new CccdVerificationResult(true, "TEST STAFF USER", new DateOnly(1998, 12, 10), "F"),
    };

    public Task<CccdVerificationResult> VerifyAsync(string cccd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = Registry.TryGetValue(cccd, out var match)
            ? match
            : new CccdVerificationResult(false, null, null, null);
        return Task.FromResult(result);
    }
}
