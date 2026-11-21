namespace ParkingSystem.Notification.WebAPI.Grpc;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    public string GrpcEndpoint { get; set; } = "http://localhost:5001";
    public int TimeoutSeconds { get; set; } = 5;
}
