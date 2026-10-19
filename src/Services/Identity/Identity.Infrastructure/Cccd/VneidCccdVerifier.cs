using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ParkingSystem.Identity.Application.Cccd;

namespace ParkingSystem.Identity.Infrastructure.Cccd;

public sealed class VneidCccdVerifier(HttpClient httpClient) : ICccdVerifier
{
    public async Task<CccdVerificationResult> VerifyAsync(string cccd, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/vneid/cccd/verify",
            new VerifyRequest(cccd),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new CccdVerificationResult(false, null, null, null);

        var body = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken);
        if (body is null || !body.Valid)
            return new CccdVerificationResult(false, null, null, null);

        var dob = DateOnly.TryParse(body.Dob, out var parsed) ? parsed : (DateOnly?)null;
        return new CccdVerificationResult(true, body.FullName, dob, body.Gender);
    }

    private sealed record VerifyRequest(string Cccd);

    private sealed record VerifyResponse(
        [property: JsonPropertyName("valid")] bool Valid,
        [property: JsonPropertyName("fullName")] string? FullName,
        [property: JsonPropertyName("dob")] string? Dob,
        [property: JsonPropertyName("gender")] string? Gender);
}
