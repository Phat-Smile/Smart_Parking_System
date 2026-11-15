using System.Text.Json.Serialization;

namespace ParkingSystem.Parking.Application.ParkingLots.Recommendations;

public sealed record CandidateSnapshotInputDto(
    [property: JsonPropertyName("lotId")] Guid LotId,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("capturedAt")] DateTimeOffset CapturedAt,
    [property: JsonPropertyName("availableSlots")] int AvailableSlots);

public sealed record CandidateLotInputDto(
    [property: JsonPropertyName("lotId")] Guid LotId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("priceForDuration")] double PriceForDuration,
    [property: JsonPropertyName("heldQuota")] int HeldQuota,
    [property: JsonPropertyName("heldQuotaKnown")] bool HeldQuotaKnown,
    [property: JsonPropertyName("snapshots")] IReadOnlyList<CandidateSnapshotInputDto> Snapshots);

public sealed record RecommendationClientRequest(
    [property: JsonPropertyName("customerLatitude")] double CustomerLatitude,
    [property: JsonPropertyName("customerLongitude")] double CustomerLongitude,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("arrivalAt")] DateTimeOffset ArrivalAt,
    [property: JsonPropertyName("durationMinutes")] int DurationMinutes,
    [property: JsonPropertyName("candidateLots")] IReadOnlyList<CandidateLotInputDto> CandidateLots);

public sealed record RecommendedLotItemDto(
    [property: JsonPropertyName("lotId")] Guid LotId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("driveTimeMinutes")] double? DriveTimeMinutes,
    [property: JsonPropertyName("probabilityPercent")] double? ProbabilityPercent,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("warningHighFullRisk")] bool WarningHighFullRisk,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
    [property: JsonPropertyName("sampleCount")] int SampleCount = 0,
    [property: JsonPropertyName("insufficientData")] bool InsufficientData = false,
    [property: JsonPropertyName("baselineSource")] string? BaselineSource = null);

public sealed record RecommendationClientResponse(
    [property: JsonPropertyName("recommendedLots")] IReadOnlyList<RecommendedLotItemDto> RecommendedLots,
    [property: JsonPropertyName("fallbackUsed")] bool FallbackUsed,
    [property: JsonPropertyName("fallbackReason")] string? FallbackReason);

public interface IParkingRecommendationClient
{
    Task<RecommendationClientResponse?> GetRecommendationsAsync(
        RecommendationClientRequest request,
        CancellationToken cancellationToken);
}
