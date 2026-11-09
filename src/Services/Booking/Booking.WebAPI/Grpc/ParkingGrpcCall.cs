using Grpc.Core;
using ParkingSystem.AspNetCore.Security;

namespace ParkingSystem.Booking.WebAPI.Grpc;

// Shared call setup for the Booking-to-Parking gRPC providers.
internal static class ParkingGrpcCall
{
    private const int DefaultTimeoutSeconds = 5;

    public static Metadata Headers(ServiceKeyOptions serviceKey)
    {
        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(serviceKey.ServiceKey))
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey.ServiceKey);
        return headers;
    }

    public static DateTime Deadline(ParkingServiceOptions options) =>
        DateTime.UtcNow.AddSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : DefaultTimeoutSeconds);

    // NOT_FOUND and FAILED_PRECONDITION are expected states (unapproved lot, no price list); the rest are faults.
    public static void LogFailure(ILogger logger, RpcException ex, Guid lotId)
    {
        if (ex.StatusCode is StatusCode.NotFound or StatusCode.FailedPrecondition)
            logger.LogWarning("Parking has no usable data for lot {LotId}: {Status} {Detail}.", lotId, ex.StatusCode, ex.Status.Detail);
        else
            logger.LogError(ex, "Parking gRPC call failed with {Status} for lot {LotId}.", ex.StatusCode, lotId);
    }
}
