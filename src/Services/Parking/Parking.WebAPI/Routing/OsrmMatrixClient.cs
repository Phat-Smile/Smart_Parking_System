using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;

namespace ParkingSystem.Parking.WebAPI.Routing;

/// <summary>
/// US-SEARCH-006 (FR-MAP-002): one OSRM Table request for up to ten candidates.
/// Failure returns no travel durations: a straight-line distance is never labelled as a route.
/// No customer coordinates are written to logs or persistent storage.
/// </summary>
public sealed class OsrmMatrixClient(HttpClient client, IMemoryCache cache, IConfiguration configuration)
{
    public async Task<IReadOnlyDictionary<Guid, double?>> GetMinutesAsync(
        decimal originLatitude, decimal originLongitude,
        IReadOnlyList<CustomerLotSearchItem> candidates,
        string vehicleGroup,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return new Dictionary<Guid, double?>();
        if (candidates.Count > 10) throw new ArgumentOutOfRangeException(nameof(candidates), "Maximum ten routing destinations.");

        // Canonical ordering allows a 60-second cache hit even when result ordering changes.
        var ordered = candidates.OrderBy(x => x.Id).ToArray();
        static string Rounded(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero)
            .ToString("F3", CultureInfo.InvariantCulture);
        var cacheKey = "osrm-table-v1:" + Rounded(originLatitude) + ":" + Rounded(originLongitude)
            + ":" + vehicleGroup + ":" + string.Join(";", ordered.Select(x =>
                x.Id.ToString("N") + "," + Rounded(x.Latitude) + "," + Rounded(x.Longitude)));
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
                result[ordered[i].Id] = duration;
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
