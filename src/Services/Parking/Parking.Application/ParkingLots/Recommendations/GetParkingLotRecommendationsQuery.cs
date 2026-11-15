using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLots.Recommendations;

public sealed record GetParkingLotRecommendationsQuery(
    decimal Latitude,
    decimal Longitude,
    string VehicleType = "Car",
    DateTimeOffset? ArrivalAt = null,
    int DurationMinutes = 120,
    double RadiusKm = 10.0,
    int TopK = 3) : IRequest<ParkingLotRecommendationResult>;

public sealed record ParkingLotRecommendationResult(
    IReadOnlyList<RecommendedParkingLotDto> RecommendedLots,
    bool FallbackUsed,
    string? FallbackReason);

public sealed record RecommendedParkingLotDto(
    Guid LotId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    int Rank,
    double? DriveTimeMinutes,
    double? ProbabilityPercent,
    decimal Price,
    bool WarningHighFullRisk,
    IReadOnlyList<string> Reasons,
    double DistanceKm,
    int SampleCount = 0,
    bool InsufficientData = false,
    string? BaselineSource = null);

public sealed class GetParkingLotRecommendationsQueryValidator : AbstractValidator<GetParkingLotRecommendationsQuery>
{
    public GetParkingLotRecommendationsQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.RadiusKm).GreaterThan(0).LessThanOrEqualTo(50);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 1440);
        RuleFor(x => x.TopK).InclusiveBetween(1, 10);
        RuleFor(x => x.VehicleType)
            .Must(x => x.Equals("Car", StringComparison.OrdinalIgnoreCase) ||
                       x.Equals("Motorbike", StringComparison.OrdinalIgnoreCase))
            .WithMessage("VehicleType must be either 'Car' or 'Motorbike'.");
    }
}

public sealed class GetParkingLotRecommendationsQueryHandler(
    INearbyParkingLotReader nearbyReader,
    ICustomerLotMetricsReader metricsReader,
    IArrivalSnapshotReader snapshotReader,
    IHeldQuotaProvider heldQuotaProvider,
    IParkingRecommendationClient recommendationClient,
    ILogger<GetParkingLotRecommendationsQueryHandler> logger)
    : IRequestHandler<GetParkingLotRecommendationsQuery, ParkingLotRecommendationResult>
{
    private const decimal DefaultHourlyRate = 10000m;

    public async Task<ParkingLotRecommendationResult> Handle(
        GetParkingLotRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var isCar = request.VehicleType.Equals("Car", StringComparison.OrdinalIgnoreCase);
        var domainVehicleType = isCar ? VehicleType.Car : VehicleType.Motorbike;
        var vehicleTypeStr = isCar ? "Car" : "Motorbike";
        var arrivalAt = request.ArrivalAt ?? DateTimeOffset.UtcNow.AddMinutes(30);

        // 1. Search candidate lots within radius
        var nearbyLots = await nearbyReader.SearchAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            limit: 10,
            cancellationToken,
            domainVehicleType);

        if (nearbyLots.Count == 0)
        {
            return new ParkingLotRecommendationResult([], false, null);
        }

        var candidateIds = nearbyLots.Select(x => x.Id).Distinct().ToList();
        var metrics = await metricsReader.ReadAsync(candidateIds, cancellationToken);

        // 2. Fetch held quotas in parallel via IHeldQuotaProvider (does not use ParkingDbContext, avoids sequential timeout)
        var durationHours = Math.Max(1.0, request.DurationMinutes / 60.0);
        var heldQuotaTasks = nearbyLots.Select(async lot =>
        {
            var held = await heldQuotaProvider.GetHeldReservationsAsync(lot.Id, cancellationToken);
            return (LotId: lot.Id, Held: held);
        });
        var heldQuotaMap = (await Task.WhenAll(heldQuotaTasks)).ToDictionary(x => x.LotId, x => x.Held);

        // 3. Fetch snapshots SEQUENTIALLY on ParkingDbContext to prevent concurrent EF Core queries on same DbContext instance
        var candidateMap = nearbyLots.ToDictionary(x => x.Id);
        var candidateInputs = new List<CandidateLotInputDto>();

        foreach (var lot in nearbyLots)
        {
            metrics.TryGetValue(lot.Id, out var metric);
            var baseHourlyRate = GetBaseHourlyRate(metric, vehicleTypeStr);
            var priceForDuration = (double)Math.Round(baseHourlyRate * (decimal)durationHours);

            var snapshots = await snapshotReader.GetSessionProxyAsync(
                lot.Id,
                vehicleTypeStr,
                arrivalAt,
                cancellationToken);

            var snapshotDtos = snapshots.Select(s => new CandidateSnapshotInputDto(
                s.LotId,
                s.VehicleType,
                s.CapturedAt,
                s.AvailableSlots)).ToList();

            var held = heldQuotaMap.TryGetValue(lot.Id, out var h) ? h : null;

            candidateInputs.Add(new CandidateLotInputDto(
                lot.Id,
                lot.Name,
                (double)lot.Latitude,
                (double)lot.Longitude,
                priceForDuration,
                HeldQuota: held ?? 0,
                HeldQuotaKnown: held.HasValue,
                Snapshots: snapshotDtos));
        }

        // 4. Call AI recommendation client with strict timeout
        var aiRequest = new RecommendationClientRequest(
            (double)request.Latitude,
            (double)request.Longitude,
            vehicleTypeStr,
            arrivalAt,
            request.DurationMinutes,
            candidateInputs);

        var aiResponse = await recommendationClient.GetRecommendationsAsync(aiRequest, cancellationToken);

        // 5. If AI service responded successfully, map and return results
        if (aiResponse is not null && aiResponse.RecommendedLots.Count > 0)
        {
            var recommendedLots = new List<RecommendedParkingLotDto>();
            foreach (var item in aiResponse.RecommendedLots.Take(request.TopK))
            {
                if (!candidateMap.TryGetValue(item.LotId, out var candidate))
                    continue;

                recommendedLots.Add(new RecommendedParkingLotDto(
                    candidate.Id,
                    candidate.Name,
                    candidate.Address,
                    candidate.Latitude,
                    candidate.Longitude,
                    item.Rank,
                    item.DriveTimeMinutes,
                    item.ProbabilityPercent,
                    (decimal)item.Price,
                    item.WarningHighFullRisk,
                    item.Reasons,
                    candidate.DistanceKm,
                    item.SampleCount,
                    item.InsufficientData,
                    item.BaselineSource));
            }

            if (recommendedLots.Count > 0)
            {
                return new ParkingLotRecommendationResult(
                    recommendedLots,
                    aiResponse.FallbackUsed,
                    aiResponse.FallbackReason);
            }
        }

        // 5. Fail-Closed Fallback: Rank by Haversine distance & capacity (BR-08.f)
        logger.LogWarning("Engaging fail-closed Haversine & capacity fallback for Top-3 recommendation.");
        var fallbackLots = BuildFailClosedFallback(
            nearbyLots,
            metrics,
            isCar,
            vehicleTypeStr,
            request.DurationMinutes,
            request.TopK);

        return new ParkingLotRecommendationResult(
            fallbackLots,
            FallbackUsed: true,
            FallbackReason: "ai_service_timeout_or_unavailable");
    }

    private static decimal GetBaseHourlyRate(CustomerLotMetrics? metric, string vehicleTypeStr)
    {
        if (metric is null || metric.Prices.Count == 0)
            return DefaultHourlyRate;

        var matchingPrice = metric.Prices
            .FirstOrDefault(p => p.VehicleType.Equals(vehicleTypeStr, StringComparison.OrdinalIgnoreCase));

        return matchingPrice?.Price ?? DefaultHourlyRate;
    }

    private static IReadOnlyList<RecommendedParkingLotDto> BuildFailClosedFallback(
        IReadOnlyList<NearbyParkingLotItem> candidates,
        IReadOnlyDictionary<Guid, CustomerLotMetrics> metrics,
        bool isCar,
        string vehicleTypeStr,
        int durationMinutes,
        int topK)
    {
        var durationHours = Math.Max(1.0, durationMinutes / 60.0);

        var ranked = candidates
            .Select(lot =>
            {
                metrics.TryGetValue(lot.Id, out var metric);
                var availableSpaces = isCar ? (metric?.AvailableCars ?? 0) : (metric?.AvailableMotorbikes ?? 0);
                var baseRate = GetBaseHourlyRate(metric, vehicleTypeStr);
                var price = Math.Round(baseRate * (decimal)durationHours);
                var driveTimeMinutes = Math.Max(2.0, Math.Round(lot.DistanceKm / 30.0 * 60.0, 1));

                return new
                {
                    Lot = lot,
                    AvailableSpaces = availableSpaces,
                    Price = price,
                    DriveTimeMinutes = driveTimeMinutes,
                    WarningHighFullRisk = availableSpaces <= 1
                };
            })
            // Multi-criteria heuristic: distance primary, available space secondary
            .OrderBy(x => x.Lot.DistanceKm)
            .ThenByDescending(x => x.AvailableSpaces)
            .Take(topK)
            .ToList();

        var result = new List<RecommendedParkingLotDto>();
        for (var i = 0; i < ranked.Count; i++)
        {
            var item = ranked[i];
            var rank = i + 1;
            var reasons = GenerateFallbackReasons(rank, item.Lot.DistanceKm, item.AvailableSpaces, item.Price);

            result.Add(new RecommendedParkingLotDto(
                item.Lot.Id,
                item.Lot.Name,
                item.Lot.Address,
                item.Lot.Latitude,
                item.Lot.Longitude,
                rank,
                item.DriveTimeMinutes,
                ProbabilityPercent: null,
                item.Price,
                item.WarningHighFullRisk,
                reasons,
                item.Lot.DistanceKm,
                SampleCount: 0,
                InsufficientData: true,
                BaselineSource: "fallback"));
        }

        return result;
    }

    private static IReadOnlyList<string> GenerateFallbackReasons(
        int rank,
        double distanceKm,
        int availableSpaces,
        decimal price)
    {
        return rank switch
        {
            1 => ["Gần nhất", $"{distanceKm:F1} km", availableSpaces > 0 ? "Đang có chỗ trống" : "Sắp đầy"],
            2 => ["Giá tối ưu", $"{price:N0} đ", availableSpaces > 0 ? "Khả năng còn chỗ cao" : "Cần kiểm tra"],
            _ => ["Bãi đỗ phù hợp", $"{distanceKm:F1} km"]
        };
    }
}
