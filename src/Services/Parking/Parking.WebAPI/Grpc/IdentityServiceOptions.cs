namespace ParkingSystem.Parking.WebAPI.Grpc;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    /// <summary>
    /// The gRPC base endpoint of the Identity service (for example "http://identity-api:8081").
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Deadline in seconds for one plate lookup. The gate waits for the answer, so keep it short. Defaults to 2 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 2;
}
