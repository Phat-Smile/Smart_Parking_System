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
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("AI recommendation service key is not configured; using non-AI fallback.");
            return null;
        }

        // The original 700ms budget always cancelled a provider with a 3s LLM
        // timeout before it could reply. Use a bounded, configurable budget.
        var configuredTimeout = config.GetValue<int?>("AiService:RecommendationTimeoutMs") ?? 5000;
        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(configuredTimeout, 100, 15000));

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{aiUrl}/internal/ai/recommendations")
            {
                Content = JsonContent.Create(request, options: Options)
            };

            httpRequest.Headers.TryAddWithoutValidation("X-Arrival-Service-Key", key);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI recommendation request returned status code {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RecommendationClientResponse>(Options, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // A real caller cancellation must not be converted into a fallback.
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "AI recommendation request timed out after {TimeoutMs}ms.", timeout.TotalMilliseconds);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to call AI recommendation service, triggering fail-closed fallback.");
            return null;
        }
    }
}
