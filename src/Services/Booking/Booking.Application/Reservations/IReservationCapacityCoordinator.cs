using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// Coordinates atomic capacity evaluation and deduction under an exclusive database lock
/// for the specified parking lot and vehicle group (BR-02.c, US-RES-005).
/// </summary>
public interface IReservationCapacityCoordinator
{
    Task<TResult> ExecuteUnderCapacityLockAsync<TResult>(
        Guid lotId,
        VehicleGroup vehicleGroup,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);

    Task RecordCapacityUpdatedAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}
