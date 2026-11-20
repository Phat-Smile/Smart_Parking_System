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
// The no-suitable-slot report (BR-02.f, point 35 option a) uses the same keys; any failure returns null as well.
public sealed class BookingCheckInHttpClient(
    HttpClient httpClient,
    IOptions<BookingServiceOptions> options,
    IConfiguration configuration,
    ILogger<BookingCheckInHttpClient> logger) : IBookingCheckInClient, IBookingNoSlotReporter
{
    public const string CheckInPath = "/internal/booking/reservations/check-in";
    public const string NoSuitableSlotPath = "/internal/booking/reservations/no-suitable-slot";
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
            return null;

        var result = await PostAsync<CheckInApiResponse>(
            CheckInPath,
            new { LotId = lotId, PlateNumber = normalizedPlate, EntryTime = entryTime, SessionId = sessionId },
            lotId,
            cancellationToken);
        if (result is null || !result.HasReservation || !result.ReservationId.HasValue)
            return null;

        return new CheckedInReservation(result.ReservationId.Value, result.UserId, result.VehicleId, result.ScheduledArrivalTimeUtc);
    }

    public async Task<Guid?> ReportNoSuitableSlotAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset arrivedAt,
        CancellationToken cancellationToken)
    {
        if (lotId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedPlate))
            return null;

        var result = await PostAsync<NoSuitableSlotApiResponse>(
            NoSuitableSlotPath,
            new { LotId = lotId, PlateNumber = normalizedPlate, ArrivedAt = arrivedAt },
            lotId,
            cancellationToken);
        return result is { Cancelled: true, ReservationId: { } reservationId } ? reservationId : null;
    }

    // Null for every failure (no key, timeout, refused, not found, bad body): the gate keeps working without Booking.
    private async Task<TResponse?> PostAsync<TResponse>(string path, object body, Guid lotId, CancellationToken cancellationToken)
        where TResponse : class
    {
        var arrivalKey = configuration["ArrivalIntegration:Key"];
        if (string.IsNullOrWhiteSpace(arrivalKey))
        {
            logger.LogWarning("Booking call {Path} skipped: ArrivalIntegration:Key is not configured, so the car is handled as a walk-in.", path);
            return null;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}{path}")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
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
                    "Booking call {Path} refused with HTTP {StatusCode} for lot {LotId}: check ArrivalIntegration:Key and ServiceAuth:ServiceKey on Parking and Booking.",
                    path,
                    (int)response.StatusCode,
                    lotId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Booking call {Path} returned HTTP {StatusCode} for lot {LotId}", path, (int)response.StatusCode, lotId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Gate flow continues smoothly even if booking service is temporarily unavailable
            logger.LogWarning(ex, "Booking call {Path} failed for lot {LotId}: {ErrorMessage}", path, lotId, ex.Message);
            return null;
        }
    }

    private sealed record CheckInApiResponse(
        bool HasReservation,
        Guid? ReservationId,
        Guid? UserId,
        Guid? VehicleId,
        string? Message,
        DateTime? ScheduledArrivalTimeUtc = null);

    private sealed record NoSuitableSlotApiResponse(bool Cancelled, Guid? ReservationId);
}
