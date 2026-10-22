using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ParkingSystem.Parking.Application.Common.Mapbox;

namespace ParkingSystem.Parking.Infrastructure.Mapbox;

/// <summary>
/// Server-side, opt-in Mapbox integration. No provider payload, coordinates or token are logged.
/// Temporary geocoding results are not cached or persisted.
/// </summary>
public sealed class MapboxGeoRoutingClient(HttpClient httpClient, string accessToken) : IMapboxGeoRouting, IDisposable
{
    private const string BaseUrl = "https://api.mapbox.com";

    public void Dispose() => httpClient.Dispose();

    public async Task<MapboxGeocodedPlace?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        address = address.Trim();
        if (address.Length > 256) throw new ArgumentOutOfRangeException(nameof(address), "Address must be at most 256 characters.");

        using var response = await GetJsonAsync(
            "/search/geocode/v6/forward?q=" + Uri.EscapeDataString(address)
            + "&country=vn&limit=1&autocomplete=false&language=vi", cancellationToken);
        try
        {
            var features = response.RootElement.GetProperty("features");
            if (features.ValueKind != JsonValueKind.Array) throw new FormatException();
            if (features.GetArrayLength() == 0) return null;
            var feature = features[0];
            var point = feature.GetProperty("geometry").GetProperty("coordinates");
            if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2) throw new FormatException();
            var longitude = point[0].GetDouble();
            var latitude = point[1].GetDouble();
            ValidateCoordinates(latitude, longitude);
            var properties = feature.GetProperty("properties");
            var name = properties.TryGetProperty("full_address", out var full)
                && full.ValueKind == JsonValueKind.String ? full.GetString()
                : properties.TryGetProperty("name", out var shortName)
                    && shortName.ValueKind == JsonValueKind.String ? shortName.GetString() : address;
            return new MapboxGeocodedPlace(name ?? address, latitude, longitude);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                   or FormatException or ArgumentException)
        {
            throw new MapboxProviderException("invalid_payload");
        }
    }

    public async Task<MapboxRouteResult> DirectionsAsync(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(originLatitude, originLongitude);
        ValidateCoordinates(destinationLatitude, destinationLongitude);
        var path = $"/directions/v5/mapbox/driving/{Point(originLatitude, originLongitude)};"
                 + $"{Point(destinationLatitude, destinationLongitude)}?overview=false&alternatives=false";
        using var result = await GetJsonAsync(path, cancellationToken);
        try
        {
            var body = result.RootElement;
            var code = body.GetProperty("code").GetString();
            if (code == "NoRoute") return new MapboxRouteResult(false, null, null);
            if (code != "Ok") throw new FormatException();
            var routes = body.GetProperty("routes");
            if (routes.ValueKind != JsonValueKind.Array || routes.GetArrayLength() == 0) throw new FormatException();
            var route = routes[0];
            return new MapboxRouteResult(true,
                ValidMeasure(route.GetProperty("duration")), ValidMeasure(route.GetProperty("distance")));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                   or FormatException or ArgumentException)
        {
            throw new MapboxProviderException("invalid_payload");
        }
    }

    public async Task<IReadOnlyList<MapboxRouteResult>> MatrixAsync(
        double originLatitude, double originLongitude,
        IReadOnlyList<(double Latitude, double Longitude)> destinations,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(originLatitude, originLongitude);
        ArgumentNullException.ThrowIfNull(destinations);
        if (destinations.Count is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(destinations), "Use 1 to 10 destinations per call.");
        foreach (var p in destinations) ValidateCoordinates(p.Latitude, p.Longitude);
        var points = string.Join(";", new[] { Point(originLatitude, originLongitude) }
            .Concat(destinations.Select(p => Point(p.Latitude, p.Longitude))));
        var destinationIndices = string.Join(";", Enumerable.Range(1, destinations.Count)
            .Select(i => i.ToString(CultureInfo.InvariantCulture)));
        using var result = await GetJsonAsync(
            $"/directions-matrix/v1/mapbox/driving/{points}?sources=0&destinations={destinationIndices}&annotations=duration,distance",
            cancellationToken);
        try
        {
            var root = result.RootElement;
            if (root.GetProperty("code").GetString() != "Ok") throw new FormatException();
            var durations = root.GetProperty("durations");
            var distances = root.GetProperty("distances");
            if (durations.GetArrayLength() != 1 || distances.GetArrayLength() != 1
                || durations[0].GetArrayLength() != destinations.Count
                || distances[0].GetArrayLength() != destinations.Count) throw new FormatException();
            var rows = new List<MapboxRouteResult>(destinations.Count);
            for (int i = 0; i < destinations.Count; i++)
            {
                var duration = NullableMeasure(durations[0][i]);
                var distance = NullableMeasure(distances[0][i]);
                rows.Add(new MapboxRouteResult(duration.HasValue && distance.HasValue, duration, distance));
            }
            return rows;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                   or FormatException or ArgumentException)
        {
            throw new MapboxProviderException("invalid_payload");
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) throw new MapboxProviderException("token_not_configured");
        // No HttpClientFactory request logging: Mapbox uses access_token in query string.
        var url = BaseUrl + path + "&access_token=" + Uri.EscapeDataString(accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("ParkWise-Backend/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var kind = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "unauthorized",
                    HttpStatusCode.Forbidden => "forbidden",
                    HttpStatusCode.TooManyRequests => "rate_limited",
                    _ => "provider_unavailable"
                };
                throw new MapboxProviderException(kind);
            }
            var data = await response.Content.ReadAsByteArrayAsync(ct);
            if (data.Length > 1024 * 1024) throw new MapboxProviderException("payload_too_large");
            return JsonDocument.Parse(data);
        }
        catch (HttpRequestException) { throw new MapboxProviderException("network_error"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new MapboxProviderException("timeout"); }
        catch (JsonException) { throw new MapboxProviderException("invalid_payload"); }
    }

    private static string Point(double latitude, double longitude) =>
        longitude.ToString("F6", CultureInfo.InvariantCulture) + "," +
        latitude.ToString("F6", CultureInfo.InvariantCulture);

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Invalid latitude/longitude.");
    }

    private static double ValidMeasure(JsonElement value) => NullableMeasure(value) ?? throw new FormatException();
    private static double? NullableMeasure(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var number = value.GetDouble();
        if (!double.IsFinite(number) || number < 0) throw new FormatException();
        return number;
    }
}

/// <summary>Only a safe error code is exposed (never Mapbox URLs or tokens).</summary>
public sealed class MapboxProviderException(string code) : Exception(code)
{
    public string Code => Message;
}
