namespace ParkingSystem.Booking.WebAPI.Grpc;

public sealed class ParkingServiceOptions
{
    public const string SectionName = "ParkingService";

    /// <summary>
    /// The gRPC base endpoint for the Parking service (e.g. "http://parking-api:8081").
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds for calls to the Parking service. Defaults to 5 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
