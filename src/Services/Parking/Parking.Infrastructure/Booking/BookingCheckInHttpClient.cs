using System.Net.Http.Json;
using System.Text.Json;
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

public sealed class BookingCheckInHttpClient(
    HttpClient httpClient,
    IOptions<BookingServiceOptions> options,
    ILogger<BookingCheckInHttpClient> logger) : IBookingCheckInClient
{
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

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var baseUrl = options.Value.BaseUrl.TrimEnd('/');
            var requestUri = $"{baseUrl}/api/booking/reservations/check-in";

            var payload = new
            {
                LotId = lotId,
                PlateNumber = normalizedPlate,
                EntryTime = entryTime,
                SessionId = sessionId
            };

            var response = await httpClient.PostAsJsonAsync(requestUri, payload, JsonOptions, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Booking check-in endpoint returned HTTP {StatusCode} for lot {LotId}", response.StatusCode, lotId);
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
