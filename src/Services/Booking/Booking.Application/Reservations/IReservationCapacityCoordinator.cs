using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// Port abstraction for atomic capacity coordination and reservation creation.
/// Implemented in Persistence using PostgreSQL row-level locks (SELECT ... FOR UPDATE)
/// inside a single READ COMMITTED transaction.
/// </summary>
public interface IReservationCapacityCoordinator
{
    /// <summary>
    /// Atomically validates ceiling (condition i) and available slots (condition ii) under a row lock
    /// on (lot_id, vehicle_group) in booking.reservation_capacity, and inserts the reservation in TemporaryHold status.
    /// Supports idempotency: if reservation with same Id already exists, returns existing reservation.
    /// </summary>
    Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
        Reservation reservation,
        int ceiling,
        int? availableSlotCount,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default);
}

