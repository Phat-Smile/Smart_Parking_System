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
    /// Atomically validates user reservation limit (BR-1), plate overlap (BR-2), ceiling (condition i),
    /// and available slots (condition ii) under serialization locks within a single transaction,
    /// and inserts the reservation in TemporaryHold status.
    /// Supports idempotency: if reservation with same Id already exists, returns existing reservation.
    /// </summary>
    Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
        Reservation reservation,
        int ceiling,
        int? availableSlotCount,
        TimeSpan lockTimeout,
        int maxActiveReservationsPerUser = 3,
        CancellationToken cancellationToken = default);
}

