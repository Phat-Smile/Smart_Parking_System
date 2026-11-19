using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
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
    int TopK = 3,
    string Priority = "balanced") : IRequest<ParkingLotRecommendationResult>;

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
        RuleFor(x => x.Priority)
            .Must(priority => priority is "balanced" or "cheapest" or "fastest" or "availability")
            .WithMessage("Priority must be balanced, cheapest, fastest, or availability.");
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
    IArrivalHeldQuotaClient arrivalHeldQuotaClient,
    IParkingRecommendationClient recommendationClient,
    ILogger<GetParkingLotRecommendationsQueryHandler> logger)
    : IRequestHandler<GetParkingLotRecommendationsQuery, ParkingLotRecommendationResult>
{
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

        // Never invent a missing price (the old code substituted 10,000 VND).
        // Only send lots with an applicable PerEntry/Block policy to the LLM.
        var pricedLots = nearbyLots.Where(lot =>
            metrics.TryGetValue(lot.Id, out var metric) &&
            GetPriceForDuration(metric, vehicleTypeStr, request.DurationMinutes).HasValue).ToList();
        if (pricedLots.Count == 0)
        {
            return new ParkingLotRecommendationResult([], true, "no_applicable_pricing_policy");
        }

        // 2. Fetch held quotas in parallel (no ParkingDbContext usage here).
        var heldQuotaTasks = pricedLots.Select(async lot =>
        {
            // Booking holds car reservations only. Motorbike zero is known by policy.
            var held = isCar
                ? await arrivalHeldQuotaClient.GetHeldQuotaAsync(lot.Id, arrivalAt, cancellationToken)
                : 0;
            if (held is < 0)
                held = null;
            return (LotId: lot.Id, Held: held);
        });
        var heldQuotaMap = (await Task.WhenAll(heldQuotaTasks)).ToDictionary(x => x.LotId, x => x.Held);
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Fetch snapshots SEQUENTIALLY on ParkingDbContext to prevent concurrent EF Core queries on same DbContext instance
        var candidateMap = pricedLots.ToDictionary(x => x.Id);
        var candidateInputs = new List<CandidateLotInputDto>();

        foreach (var lot in pricedLots)
        {
            metrics.TryGetValue(lot.Id, out var metric);
            var priceForDuration = (double)GetPriceForDuration(metric, vehicleTypeStr,
                request.DurationMinutes)!.Value;

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
            candidateInputs,
            request.Priority);

        var aiResponse = await recommendationClient.GetRecommendationsAsync(aiRequest, cancellationToken);

        // 5. Validate the *entire* AI result. Never silently drop unknown IDs or
        // accept invented prices/quality numbers before returning recommendations.
        // The AI Service has already checked the model's JSON; this is our
        // service-to-service trust boundary, not a second ranking algorithm.
        var inputMap = candidateInputs.ToDictionary(x => x.LotId);
        if (aiResponse is not null && IsValidAiResponse(aiResponse, inputMap))
        {
            var recommendedLots = aiResponse.RecommendedLots.Take(request.TopK)
                .Select(item =>
                {
                    var candidate = candidateMap[item.LotId];
                    var input = inputMap[item.LotId];
                    return new RecommendedParkingLotDto(
                        candidate.Id,
                        candidate.Name,
                        candidate.Address,
                        candidate.Latitude,
                        candidate.Longitude,
                        item.Rank,
                        item.DriveTimeMinutes,
                        item.ProbabilityPercent,
                        // Use the price that Parking calculated, not the service echo.
                        (decimal)input.PriceForDuration,
                        item.WarningHighFullRisk,
                        item.Reasons,
                        candidate.DistanceKm,
                        item.SampleCount,
                        item.InsufficientData,
                        item.BaselineSource);
                }).ToList();

            return new ParkingLotRecommendationResult(
                recommendedLots,
                aiResponse.FallbackUsed,
                aiResponse.FallbackReason);
        }

        var fallbackReason = aiResponse switch
        {
            null => "ai_service_timeout_or_unavailable",
            { FallbackUsed: true, RecommendedLots.Count: 0 } => "ai_recommendation_unavailable",
            _ => "ai_response_invalid"
        };
        if (fallbackReason == "ai_response_invalid")
        {
            logger.LogWarning("Rejected invalid AI recommendation response; returning non-AI fallback.");
        }

        // 6. Clearly labelled non-AI fallback; do not present it as LLM ranking.
        logger.LogWarning("Engaging fail-closed Haversine & capacity fallback for Top-3 recommendation. Reason={FallbackReason}", fallbackReason);
        var fallbackLots = BuildFailClosedFallback(
            pricedLots,
            metrics,
            isCar,
            vehicleTypeStr,
            request.DurationMinutes,
            request.TopK);

        return new ParkingLotRecommendationResult(
            fallbackLots,
            FallbackUsed: true,
            FallbackReason: fallbackReason);
    }

    private static bool IsValidAiResponse(
        RecommendationClientResponse response,
        IReadOnlyDictionary<Guid, CandidateLotInputDto> inputs)
    {
        // AI Service returns at most 3, even if the public endpoint's TopK is larger.
        if (response.FallbackUsed || response.FallbackReason is not null ||
            response.RecommendedLots is null ||
            response.RecommendedLots.Count != Math.Min(3, inputs.Count))
            return false;

        var seen = new HashSet<Guid>();
        for (var index = 0; index < response.RecommendedLots.Count; index++)
        {
            var item = response.RecommendedLots[index];
            if (item is null || item.Rank != index + 1 || !seen.Add(item.LotId) ||
                !inputs.TryGetValue(item.LotId, out var input))
                return false;

            if (!string.Equals(item.Name, input.Name, StringComparison.Ordinal) ||
                !double.IsFinite(item.Price) ||
                Math.Abs(item.Price - input.PriceForDuration) > 0.01 ||
                item.SampleCount < 0 ||
                item.Reasons is null || item.Reasons.Count is < 1 or > 5 ||
                item.Reasons.Any(reason => string.IsNullOrWhiteSpace(reason) || reason.Length > 400))
                return false;

            if (item.DriveTimeMinutes is double time && (!double.IsFinite(time) || time < 0))
                return false;

            if (item.ProbabilityPercent is double probability &&
                (!double.IsFinite(probability) || probability is < 0 or > 100 ||
                 item.InsufficientData || !input.HeldQuotaKnown))
                return false;

            // Prevent an AI response from claiming enough data while hiding its probability.
            if (item.ProbabilityPercent is null && !item.InsufficientData)
                return false;
        }

        return true;
    }

    private static decimal? GetPriceForDuration(
        CustomerLotMetrics? metric, string vehicleTypeStr, int durationMinutes)
    {
        var policy = metric?.Prices.FirstOrDefault(p =>
            p.VehicleType.Equals(vehicleTypeStr, StringComparison.OrdinalIgnoreCase));
        if (policy is null || policy.Price <= 0)
            return null;

        if (policy.BillingMode.Equals("PerEntry", StringComparison.OrdinalIgnoreCase))
            return policy.Price;

        if (policy.BillingMode.Equals("Block", StringComparison.OrdinalIgnoreCase) &&
            policy.BlockMinutes is > 0)
        {
            return Math.Ceiling((decimal)durationMinutes / policy.BlockMinutes.Value) * policy.Price;
        }

        // Unsupported or incomplete policies must not be treated as hourly prices.
        return null;
    }

    private static IReadOnlyList<RecommendedParkingLotDto> BuildFailClosedFallback(
        IReadOnlyList<NearbyParkingLotItem> candidates,
        IReadOnlyDictionary<Guid, CustomerLotMetrics> metrics,
        bool isCar,
        string vehicleTypeStr,
        int durationMinutes,
        int topK)
    {
        var ranked = candidates
            .Select(lot =>
            {
                metrics.TryGetValue(lot.Id, out var metric);
                var availableSpaces = isCar ? (metric?.AvailableCars ?? 0) : (metric?.AvailableMotorbikes ?? 0);
                var price = GetPriceForDuration(metric, vehicleTypeStr, durationMinutes)!.Value;
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
            1 => ["Gần nhất theo khoảng cách", $"{distanceKm:F1} km",
                availableSpaces > 0 ? "Đang báo còn chỗ" : "Chưa xác nhận còn chỗ"],
            2 => ["Bãi gần khu vực", $"Giá ước tính {price:N0} đ",
                availableSpaces > 0 ? "Đang báo còn chỗ" : "Cần kiểm tra chỗ trống"],
            _ => ["Bãi lân cận (không phải AI)", $"{distanceKm:F1} km"]
        };
    }
}
