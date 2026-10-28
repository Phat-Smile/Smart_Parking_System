using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ParkingSystem.Parking.Application.ParkingLots;

namespace ParkingSystem.Parking.WebAPI.Geocoding;

// This limiter is shared among requests to ONE API process. Do not scale this
// public-Nominatim adapter to multiple API replicas without a distributed gate.
public sealed class NominatimRequestGate
{
    private readonly object _sync = new();
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public bool TryAcquire()
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _nextAllowed)
                return false;
            _nextAllowed = now.AddSeconds(1);
            return true;
        }
    }
}

public sealed class GeocodingRateLimitedException : Exception { }
public sealed class GeocodingProviderUnavailableException : Exception { }

public sealed class NominatimAddressGeocoder(
    HttpClient httpClient,
    IMemoryCache cache,
    NominatimRequestGate requestGate) : IAddressGeocoder
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromMinutes(15);

    public async Task<GeocodedDestination?> GeocodeAsync(
        string address, CancellationToken cancellationToken)
    {
        var key = "nominatim:v1:" + address.Trim().ToUpperInvariant();
        if (cache.TryGetValue<CachedGeocode>(key, out var cached))
            return cached!.Destination;

        if (!requestGate.TryAcquire())
            throw new GeocodingRateLimitedException();

        try
        {
            var url = "search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(address);
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new GeocodingProviderUnavailableException();

            var candidates = await response.Content.ReadFromJsonAsync<List<NominatimPlace>>(
                cancellationToken: cancellationToken);
            var first = candidates?.FirstOrDefault();
            if (first is null)
            {
                cache.Set(key, new CachedGeocode(null), NotFoundTtl);
                return null;
            }

            if (string.IsNullOrWhiteSpace(first.DisplayName)
                || !decimal.TryParse(first.Latitude, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var latitude)
                || !decimal.TryParse(first.Longitude, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var longitude)
                || latitude is < -90 or > 90
                || longitude is < -180 or > 180)
            {
                throw new GeocodingProviderUnavailableException();
            }

            var destination = new GeocodedDestination(first.DisplayName, latitude, longitude);
            cache.Set(key, new CachedGeocode(destination), CacheTtl);
            return destination;
        }
        catch (HttpRequestException)
        {
            throw new GeocodingProviderUnavailableException();
        }
        catch (JsonException)
        {
            throw new GeocodingProviderUnavailableException();
        }
        catch (NotSupportedException)
        {
            throw new GeocodingProviderUnavailableException();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeocodingProviderUnavailableException();
        }
    }

    private sealed record CachedGeocode(GeocodedDestination? Destination);

    private sealed class NominatimPlace
    {
        public NominatimPlace() { }
        [System.Text.Json.Serialization.JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lat")]
        public string? Latitude { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lon")]
        public string? Longitude { get; set; }
    }
}
