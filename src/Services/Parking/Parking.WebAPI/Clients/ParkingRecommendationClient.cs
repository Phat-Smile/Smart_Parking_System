using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;

namespace ParkingSystem.Parking.WebAPI.Clients;

public sealed class ParkingRecommendationClient(
    HttpClient client,
    IConfiguration config,
    ILogger<ParkingRecommendationClient> logger) : IParkingRecommendationClient
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<RecommendationClientResponse?> GetRecommendationsAsync(
        RecommendationClientRequest request,
        CancellationToken cancellationToken)
    {
        var key = config["ArrivalIntegration:Key"] ?? config["AiService:ApiKey"];
        var aiUrl = (config["AiService:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{aiUrl}/internal/ai/recommendations")
            {
                Content = JsonContent.Create(request, options: Options)
            };

            if (!string.IsNullOrWhiteSpace(key))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Arrival-Service-Key", key);
            }

            // Enforce strict 700ms timeout for AI service call (US-AI-001/002)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(700));

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI recommendation request returned status code {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RecommendationClientResponse>(Options, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AI recommendation request timed out after 700ms, triggering fail-closed fallback.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to call AI recommendation service, triggering fail-closed fallback.");
            return null;
        }
    }
}
