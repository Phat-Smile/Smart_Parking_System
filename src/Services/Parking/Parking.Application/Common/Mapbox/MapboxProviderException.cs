namespace ParkingSystem.Parking.Application.Common.Mapbox;

/// <summary>Only a safe error code is exposed (never Mapbox URLs or tokens).</summary>
public sealed class MapboxProviderException(string code) : Exception(code)
{
    public string Code => Message;
}
