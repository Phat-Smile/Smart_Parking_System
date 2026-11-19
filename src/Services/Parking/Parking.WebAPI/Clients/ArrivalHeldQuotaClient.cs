using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.ParkingLots.Availability;

namespace ParkingSystem.Parking.WebAPI.Clients;

/// <summary>Uses the existing Booking arrival-availability contract without the Gate quota cache.</summary>
public sealed class ArrivalHeldQuotaClient(
    HttpClient client,
    IConfiguration config,
    ILogger<ArrivalHeldQuotaClient> logger) : IArrivalHeldQuotaClient
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<int?> GetHeldQuotaAsync(Guid lotId, DateTimeOffset arrivalAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = config["ArrivalIntegration:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Recommendation quota unavailable: ArrivalIntegration:Key is not configured.");
            return null;
        }

        // A bounded per-call deadline; all candidate calls run concurrently in the handler.
        var configuredTimeout = config["BookingService:ArrivalQuotaTimeoutMs"];
        var timeoutMs = Math.Clamp(int.TryParse(configuredTimeout, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsedTimeout) ? parsedTimeout : 2000, 100, 5000);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            var bookingUrl = (config["BookingService:BaseUrl"] ?? "http://localhost:5003").TrimEnd('/');
            var timestamp = Uri.EscapeDataString(arrivalAt.ToString("O", CultureInfo.InvariantCulture));
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{bookingUrl}/internal/booking/held-quota?lotId={lotId:D}&arrivalAt={timestamp}");
            request.Headers.TryAddWithoutValidation("X-Arrival-Service-Key", key);
            var serviceKey = config["ServiceAuth:ServiceKey"];
            if (!string.IsNullOrWhiteSpace(serviceKey))
                request.Headers.TryAddWithoutValidation("X-Service-Key", serviceKey);

            using var response = await client.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Recommendation quota request returned {StatusCode}.", response.StatusCode);
                return null;
            }

            var quota = await response.Content.ReadFromJsonAsync<ArrivalQuotaResult>(Options, deadline.Token);
            cancellationToken.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();
            // Nullable fields prevent missing JSON properties from silently becoming zero/default.
            if (quota is null || quota.LotId != lotId || quota.ArrivalAt != arrivalAt ||
                quota.HeldQuota is null or < 0)
            {
                logger.LogWarning("Recommendation quota response did not match the request contract.");
                return null;
            }
            return quota.HeldQuota;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Recommendation quota request timed out.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or
            InvalidOperationException or UriFormatException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("Recommendation quota request failed ({ErrorType}).", ex.GetType().Name);
            return null;
        }
    }

    private sealed record ArrivalQuotaResult(Guid? LotId, DateTimeOffset? ArrivalAt, int? HeldQuota);
}
