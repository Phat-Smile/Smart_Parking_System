using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;

namespace ParkingSystem.Parking.WebAPI.Routing;

/// <summary>
/// US-SEARCH-006 (FR-MAP-002): one OSRM Table request for up to ten candidates.
/// Failure returns no travel durations: a straight-line distance is never labelled as a route.
/// No customer coordinates are written to logs or persistent storage.
/// </summary>
public sealed class OsrmMatrixClient(HttpClient client, IMemoryCache cache, IConfiguration configuration)
    : IRecommendationDrivingEtaReader
{
    public async Task<IReadOnlyDictionary<Guid, double?>> GetMinutesAsync(
        decimal originLatitude, decimal originLongitude,
        IReadOnlyList<CustomerLotSearchItem> candidates,
        string vehicleGroup,
        CancellationToken cancellationToken)
        => await GetDrivingMinutesAsync(originLatitude, originLongitude,
            candidates.Select(x => new RecommendationRouteDestination(x.Id, x.Latitude, x.Longitude)).ToArray(),
            vehicleGroup, cancellationToken);

    internal static string BuildCacheKey(decimal originLatitude, decimal originLongitude,
        IReadOnlyList<RecommendationRouteDestination> candidates, string vehicleGroup)
    {
        // Use the identical numeric representation as the outbound OSRM URL. No raw GPS values
        // or destination coordinates are exposed in the resulting cache key.
        static string Canonical(decimal value) => value.ToString("F6", CultureInfo.InvariantCulture);
        var parts = new StringBuilder("osrm-table-v2|")
            .Append(Canonical(originLatitude)).Append('|')
            .Append(Canonical(originLongitude)).Append('|')
            .Append(vehicleGroup);
        foreach (var candidate in candidates.OrderBy(x => x.LotId))
        {
            parts.Append('|').Append(candidate.LotId.ToString("N"))
                .Append(',').Append(Canonical(candidate.Latitude))
                .Append(',').Append(Canonical(candidate.Longitude));
        }

        return "osrm-table-v2:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parts.ToString())));
    }

    public async Task<IReadOnlyDictionary<Guid, double?>> GetDrivingMinutesAsync(
        decimal originLatitude, decimal originLongitude,
        IReadOnlyList<RecommendationRouteDestination> candidates,
        string vehicleGroup,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return new Dictionary<Guid, double?>();
        if (candidates.Count > 10) throw new ArgumentOutOfRangeException(nameof(candidates), "Maximum ten routing destinations.");

        // Canonical order permits reuse by Search and Recommendation even when candidate order differs.
        // The cache key must reflect exactly the six-decimal coordinates sent to OSRM. Rounding to
        // three decimals can alias different origins (~100 m) and return someone else's ETA.
        // Hashing also avoids storing GPS values in an in-process cache key.
        var ordered = candidates.OrderBy(x => x.LotId).ToArray();
        var cacheKey = BuildCacheKey(originLatitude, originLongitude, ordered, vehicleGroup);
        if (cache.TryGetValue<IReadOnlyDictionary<Guid, double?>>(cacheKey, out var cached) && cached is not null)
            return cached;

        static string Coordinate(decimal longitude, decimal latitude) =>
            longitude.ToString("F6", CultureInfo.InvariantCulture) + "," +
            latitude.ToString("F6", CultureInfo.InvariantCulture);

        var points = string.Join(";", new[] { Coordinate(originLongitude, originLatitude) }
            .Concat(ordered.Select(x => Coordinate(x.Longitude, x.Latitude))));
        var destinations = string.Join(";", Enumerable.Range(1, ordered.Length));
        var baseUrl = (configuration["Osrm:BaseUrl"] ?? "https://router.project-osrm.org").TrimEnd('/');
        // The provider must be a trusted, configured URL. Never accept it from a client request.
        var uri = $"{baseUrl}/table/v1/driving/{points}?sources=0&destinations={destinations}&annotations=duration";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("ParkWise-OJT/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode) return new Dictionary<Guid, double?>();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 12 },
                cancellationToken);
            var root = document.RootElement;
            if (root.GetProperty("code").GetString() != "Ok") return new Dictionary<Guid, double?>();
            var durations = root.GetProperty("durations");
            if (durations.GetArrayLength() != 1 || durations[0].GetArrayLength() != ordered.Length)
                return new Dictionary<Guid, double?>();
            var result = new Dictionary<Guid, double?>(ordered.Length);
            for (var i = 0; i < ordered.Length; i++)
            {
                var seconds = durations[0][i];
                var duration = seconds.ValueKind == JsonValueKind.Number && seconds.TryGetDouble(out var value)
                    && double.IsFinite(value) && value >= 0 ? Math.Round(value / 60.0, 1) : (double?)null;
                result[ordered[i].LotId] = duration;
            }
            cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                or KeyNotFoundException or InvalidOperationException)
        {
            return new Dictionary<Guid, double?>();
        }
    }
}
