using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class ArrivalAvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapArrivalAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/lots/{lotId:guid}/arrival-availability", async Task<IResult> (
            Guid lotId, DateTimeOffset? arrivalAt, string? vehicleType,
            IParkingLotDetailReader lotReader, IArrivalSnapshotReader snapshotReader,
            ArrivalAvailabilityClient aiClient, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            if (lotId == Guid.Empty || arrivalAt is null ||
                (vehicleType != "Car" && vehicleType != "Motorbike") ||
                arrivalAt <= DateTimeOffset.UtcNow ||
                arrivalAt > DateTimeOffset.UtcNow.AddDays(30))
                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest,
                    new Error("parking.arrival_availability.invalid_parameters", "Invalid lotId, arrivalAt or vehicleType."));

            if (await lotReader.GetApprovedByIdAsync(lotId, cancellationToken) is null)
                return Results.NotFound();

            // Parking supplies snapshots; caller cannot provide quota or fabricated history.
            var snapshots = await snapshotReader.GetSessionProxyAsync(
                lotId, vehicleType, arrivalAt.Value, cancellationToken);
            var estimate = await aiClient.GetEstimateAsync(lotId, vehicleType,
                arrivalAt.Value, snapshots, cancellationToken);
            return estimate is null
                ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new
                {
                    estimate.LotId, estimate.VehicleType, estimate.ArrivalAt,
                    estimate.ProbabilityPercent, estimate.SampleCount,
                    estimate.BaselineSource, estimate.Estimated,
                    estimate.InsufficientData, estimate.Warning,
                    DataSource = "parking_session_proxy_15m",
                    DataLimitation = "Estimated from recorded sessions and current configured capacity; not historical IoT sensor measurements."
                });
        })
        .AllowAnonymous()
        .WithName("GetParkingLotArrivalAvailability")
        .WithSummary("Estimated future free-space probability for an approved parking lot")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}

// The dedicated integration key is server-side only; fail closed if unset.
public sealed class ArrivalAvailabilityClient(HttpClient client, IConfiguration config, ILogger<ArrivalAvailabilityClient> logger)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<ArrivalEstimateResult?> GetEstimateAsync(
        Guid lotId, string vehicleType, DateTimeOffset arrivalAt,
        IReadOnlyList<ArrivalAvailabilitySnapshot> snapshots, CancellationToken cancellationToken)
    {
        var key = config["ArrivalIntegration:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Arrival forecast unavailable: ArrivalIntegration:Key is not configured.");
            return null;
        }
        try
        {
            var bookingUrl = (config["BookingService:BaseUrl"] ?? "http://localhost:5003").TrimEnd('/');
            var aiUrl = (config["AiService:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
            var heldQuota = 0;
            // Booking currently supports Car reservations only; do not make a needless
            // cross-service call for motorbike estimates.
            if (vehicleType == "Car")
            {
                using var bookingRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"{bookingUrl}/internal/booking/held-quota?lotId={lotId:D}&arrivalAt={Uri.EscapeDataString(arrivalAt.ToString("O"))}");
                bookingRequest.Headers.TryAddWithoutValidation("X-Arrival-Service-Key", key);
                var globalServiceKey = config["ServiceAuth:ServiceKey"];
                if (!string.IsNullOrWhiteSpace(globalServiceKey))
                    bookingRequest.Headers.TryAddWithoutValidation("X-Service-Key", globalServiceKey);
                using var bookingResponse = await client.SendAsync(bookingRequest, cancellationToken);
                if (!bookingResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning("Arrival booking quota request returned {StatusCode}", bookingResponse.StatusCode);
                    return null;
                }
                var quota = await bookingResponse.Content.ReadFromJsonAsync<ArrivalQuotaResult>(Options, cancellationToken);
                if (quota is null || quota.LotId != lotId || quota.HeldQuota < 0)
                    return null;
                heldQuota = quota.HeldQuota;
            }

            using var aiRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{aiUrl}/internal/ai/arrival-availability");
            aiRequest.Headers.TryAddWithoutValidation("X-Arrival-Service-Key", key);
            aiRequest.Content = JsonContent.Create(new
            {
                lotId, vehicleType, arrivalAt, heldQuotaAtArrival = heldQuota,
                snapshots
            }, options: Options);
            using var aiResponse = await client.SendAsync(aiRequest, cancellationToken);
            if (!aiResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Arrival AI request returned {StatusCode}", aiResponse.StatusCode);
                return null;
            }
            var estimate = await aiResponse.Content.ReadFromJsonAsync<ArrivalEstimateResult>(Options, cancellationToken);
            return estimate is not null && estimate.LotId == lotId &&
                estimate.VehicleType == vehicleType && estimate.ProbabilityPercent is >= 0 and <= 100
                ? estimate : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Arrival forecast dependencies unavailable");
            return null; // Never return a fabricated zero quota or a fake forecast on transport errors.
        }
    }

    private sealed record ArrivalQuotaResult(Guid LotId, DateTimeOffset ArrivalAt, int HeldQuota);
    public sealed record ArrivalEstimateResult(Guid LotId, string VehicleType, DateTimeOffset ArrivalAt,
        double ProbabilityPercent, int SampleCount, string BaselineSource,
        bool Estimated, bool InsufficientData, string? Warning);
}
