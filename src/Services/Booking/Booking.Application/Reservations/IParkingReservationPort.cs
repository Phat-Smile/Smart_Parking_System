using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// TECH-05: Contract interface defining the Booking operations consumed by Parking Service.
/// 1. Use reservation at gate: switches reservation to Used state (Booking remains the owner of reservation state).
/// 2. Get held reservation count: retrieves held reservation count for lot/time/vehicle group.
/// </summary>
public interface IParkingReservationPort
{
    /// <summary>
    /// Invoked by Parking Service gate-entry flow when a vehicle with a reservation arrives at the gate (BR-03.c, BR-04.b).
    /// </summary>
    Task<UseReservationAtGateResult> UseReservationAtGateAsync(
        UseReservationAtGateRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked by Parking/Booking capacity logic to query the number of currently held reservations
    /// for a given parking lot, vehicle group, and evaluation time (BR-02.a, BR-02.e).
    /// </summary>
    Task<int> GetHeldReservationCountAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken);
}

public sealed record UseReservationAtGateRequest(
    Guid ReservationId,
    string PlateNumber,
    Guid SessionId,
    DateTimeOffset EntryTime);

public sealed record UseReservationAtGateResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    ReservationStatus? Status = null);
