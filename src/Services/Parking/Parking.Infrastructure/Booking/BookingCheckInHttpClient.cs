using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Parking.Application.Common.Booking;

namespace ParkingSystem.Parking.Infrastructure.Booking;

public sealed class BookingServiceOptions
{
    public const string SectionName = "BookingService";
    public string BaseUrl { get; set; } = "http://localhost:5003";
    public int TimeoutSeconds { get; set; } = 3;
}

// Contract 3 (US-PARK-004): Booking's check-in is internal only and needs the arrival integration key, the same key and
// header as /internal/booking/held-quota. Any failure returns null so the car goes on as a walk-in (point 34).
public sealed class BookingCheckInHttpClient(
    HttpClient httpClient,
    IOptions<BookingServiceOptions> options,
    IConfiguration configuration,
    ILogger<BookingCheckInHttpClient> logger) : IBookingCheckInClient
{
    public const string CheckInPath = "/internal/booking/reservations/check-in";
    public const string ArrivalKeyHeader = "X-Arrival-Service-Key";
    public const string ServiceKeyHeader = "X-Service-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CheckedInReservation?> CheckInReservationAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset entryTime,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (lotId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return null;
        }

        var arrivalKey = configuration["ArrivalIntegration:Key"];
        if (string.IsNullOrWhiteSpace(arrivalKey))
        {
            logger.LogWarning("Booking check-in skipped: ArrivalIntegration:Key is not configured, so the car is handled as a walk-in.");
            return null;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}{CheckInPath}")
            {
                Content = JsonContent.Create(
                    new
                    {
                        LotId = lotId,
                        PlateNumber = normalizedPlate,
                        EntryTime = entryTime,
                        SessionId = sessionId
                    },
                    options: JsonOptions)
            };
            request.Headers.TryAddWithoutValidation(ArrivalKeyHeader, arrivalKey);

            // Booking also runs the shared service-key middleware when SERVICE_KEY is set.
            var serviceKey = configuration["ServiceAuth:ServiceKey"];
            if (!string.IsNullOrWhiteSpace(serviceKey))
                request.Headers.TryAddWithoutValidation(ServiceKeyHeader, serviceKey);

            using var response = await httpClient.SendAsync(request, linkedCts.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable)
            {
                // A configuration problem, not "no reservation": the keys differ between Parking and Booking, or Booking has none.
                logger.LogError(
                    "Booking check-in refused with HTTP {StatusCode} for lot {LotId}: check ArrivalIntegration:Key and ServiceAuth:ServiceKey on Parking and Booking.",
                    (int)response.StatusCode,
                    lotId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Booking check-in endpoint returned HTTP {StatusCode} for lot {LotId}", (int)response.StatusCode, lotId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<CheckInApiResponse>(JsonOptions, linkedCts.Token);
            if (result is null || !result.HasReservation || !result.ReservationId.HasValue)
            {
                return null;
            }

            return new CheckedInReservation(result.ReservationId.Value, result.UserId, result.VehicleId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Gate flow continues smoothly even if booking service is temporarily unavailable
            logger.LogWarning(ex, "Failed to check-in reservation for lot {LotId}: {ErrorMessage}", lotId, ex.Message);
            return null;
        }
    }

    private sealed record CheckInApiResponse(
        bool HasReservation,
        Guid? ReservationId,
        Guid? UserId,
        Guid? VehicleId,
        string? Message);
}
