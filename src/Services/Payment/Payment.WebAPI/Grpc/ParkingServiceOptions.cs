namespace ParkingSystem.Payment.WebAPI.Grpc;

public sealed class ParkingServiceOptions
{
    public const string SectionName = "ParkingService";

    // gRPC base address of the Parking service, for example "http://parking-api:8081".
    public string GrpcEndpoint { get; set; } = string.Empty;

    // Per-call deadline in seconds.
    public int TimeoutSeconds { get; set; } = 5;
}
