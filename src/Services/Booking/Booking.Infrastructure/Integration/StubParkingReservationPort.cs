using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Infrastructure.Integration;

/// <summary>
/// TECH-05: Temporary STUB implementation of IParkingReservationPort.
/// Allows Booking to compile and run tests without real Parking integration.
/// 
/// IMPORTANT:
/// - This stub does NOT contact Parking service.
/// - Does NOT silently fake successful production behavior.
/// - Real Parking integration (via gRPC/events) will be implemented in future tasks.
/// </summary>
public sealed class StubParkingReservationPort : IParkingReservationPort
{
    public Task<UseReservationAtGateResult> UseReservationAtGateAsync(
        UseReservationAtGateRequest request,
        CancellationToken cancellationToken)
    {
        // Explicit placeholder stub result - indicates stub is not implemented for production
        return Task.FromResult(new UseReservationAtGateResult(
            IsSuccess: false,
            ErrorMessage: "STUB_PLACEHOLDER: Real Parking integration is not yet configured. Operation pending future gRPC contract."));
    }

    public Task<int> GetHeldReservationCountAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        // Safe placeholder: returns 0 and indicates stub behavior
        return Task.FromResult(0);
    }
}
