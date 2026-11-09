using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations;

public interface IReservationRepository
{
    Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken);

    Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
        Guid lotId,
        string plateNumber,
        DateTimeOffset arrivalTime,
        CancellationToken cancellationToken);

    void Add(Reservation reservation);

    Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken);

    Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
