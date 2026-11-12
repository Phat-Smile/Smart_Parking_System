namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// Configuration options for connecting to Payment service via gRPC.
/// </summary>
public sealed class PaymentServiceOptions
{
    public const string SectionName = "PaymentService";

    /// <summary>
    /// gRPC endpoint URI for Payment service (e.g., http://payment-api:8081).
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// gRPC call deadline timeout in seconds. Defaults to 5 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}

