using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParkingSystem.Identity.Application.Cccd;

namespace ParkingSystem.Identity.Infrastructure.Cccd;

public sealed class VneidCccdVerifier(HttpClient httpClient) : ICccdVerifier
{
    public async Task<CccdVerificationResult> VerifyAsync(string cccd, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("/vneid/cccd/verify", new VerifyRequest(cccd), cancellationToken);

            // 4xx other than throttling means the number itself was rejected; throttling and 5xx mean the provider is the problem.
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                return CccdVerificationResult.Unavailable;
            if (!response.IsSuccessStatusCode)
                return CccdVerificationResult.Invalid;

            var body = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken);
            if (body is null)
                return CccdVerificationResult.Unavailable;
            if (!body.Valid)
                return CccdVerificationResult.Invalid;

            var dob = DateOnly.TryParse(body.Dob, out var parsed) ? parsed : (DateOnly?)null;
            return CccdVerificationResult.Valid(body.FullName, dob, body.Gender);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            // Timeout, connection failure or an unreadable answer: the optional CCCD check must not break the caller.
            return CccdVerificationResult.Unavailable;
        }
    }

    private sealed record VerifyRequest(string Cccd);

    private sealed record VerifyResponse(
        [property: JsonPropertyName("valid")] bool Valid,
        [property: JsonPropertyName("fullName")] string? FullName,
        [property: JsonPropertyName("dob")] string? Dob,
        [property: JsonPropertyName("gender")] string? Gender);
}
