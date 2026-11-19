using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Infrastructure.Booking;

public sealed class BookingHeldQuotaHttpClient(
    HttpClient httpClient,
    IOptions<BookingServiceOptions> options,
    IConfiguration configuration,
    IClock clock,
    ILogger<BookingHeldQuotaHttpClient> logger) : IHeldQuotaReader
{
    public const string EndpointPath = "/internal/booking/held-quota";
    public const string ArrivalKeyHeader = "X-Arrival-Service-Key";
    public const string ServiceKeyHeader = "X-Service-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var arrivalKey = configuration["ArrivalIntegration:Key"];
        if (string.IsNullOrWhiteSpace(arrivalKey))
        {
            logger.LogWarning("Booking held-quota call skipped: ArrivalIntegration:Key is not configured.");
            throw new InvalidOperationException("ArrivalIntegration:Key is not configured.");
        }

        var baseUrl = (options.Value?.BaseUrl ?? configuration["BookingService:BaseUrl"] ?? "http://localhost:5003").TrimEnd('/');
        var arrivalAt = clock.UtcNow;
        var timestamp = Uri.EscapeDataString(arrivalAt.ToString("O", CultureInfo.InvariantCulture));
        var requestUrl = $"{baseUrl}{EndpointPath}?lotId={lotId:D}&arrivalAt={timestamp}&vehicleGroup=Car";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.TryAddWithoutValidation(ArrivalKeyHeader, arrivalKey);

        var serviceKey = configuration["ServiceAuth:ServiceKey"];
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            request.Headers.TryAddWithoutValidation(ServiceKeyHeader, serviceKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Booking held-quota call returned HTTP {StatusCode} for lot {LotId}.", (int)response.StatusCode, lotId);
                throw new HttpRequestException($"Booking held-quota endpoint returned HTTP {response.StatusCode} for lot {lotId}.", null, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<HeldQuotaApiResponse>(JsonOptions, cancellationToken);
            if (result is null || !result.HeldQuota.HasValue)
            {
                logger.LogWarning("Booking held-quota response is invalid or missing heldQuota for lot {LotId}.", lotId);
                throw new InvalidOperationException($"Invalid or empty response from Booking held-quota endpoint for lot {lotId}.");
            }

            return result.HeldQuota.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking held-quota call failed for lot {LotId}: {ErrorMessage}", lotId, ex.Message);
            throw;
        }
    }

    private sealed record HeldQuotaApiResponse(
        Guid? LotId,
        DateTimeOffset? ArrivalAt,
        string? VehicleGroup,
        int? HeldQuota);
}
