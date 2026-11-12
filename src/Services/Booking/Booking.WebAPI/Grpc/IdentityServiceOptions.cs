namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// Configuration options for connecting to Identity service via gRPC.
/// </summary>
public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    /// <summary>
    /// gRPC endpoint URI for Identity service (e.g., http://identity-api:8081).
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// gRPC call deadline timeout in seconds. Defaults to 5 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}

