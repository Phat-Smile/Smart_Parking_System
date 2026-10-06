using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

public sealed class ReservationRepository(BookingDbContext context) : IReservationRepository
{
    public async Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Reservations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> GetActiveReservationsByUserIdAsync(
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Reservations
            .Where(r => r.UserId == userId &&
                        (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed) &&
                        r.UsedAt == null &&
                        r.CancelledAt == null)
            .ToListAsync(cancellationToken);

        return candidates.Where(r => r.IsActive(utcNow)).ToList();
    }

    public async Task<IReadOnlyList<Reservation>> GetActiveReservationsByPlateAsync(
        string plateNumber,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var normalized = Reservation.NormalizePlateNumber(plateNumber);

        var candidates = await context.Reservations
            .Where(r => r.PlateNumber == normalized &&
                        (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed) &&
                        r.UsedAt == null &&
                        r.CancelledAt == null)
            .ToListAsync(cancellationToken);

        return candidates.Where(r => r.IsActive(utcNow)).ToList();
    }

    public async Task<int> CountActiveOverlappingReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        ReservationTimeWindow window,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Reservations
            .Where(r => r.LotId == lotId &&
                        r.VehicleGroup == vehicleGroup &&
                        (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed) &&
                        r.UsedAt == null &&
                        r.CancelledAt == null)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(r => r.IsActive(utcNow) && r.GetReservationWindow().Overlaps(window))
            .Count();
    }

    public async Task<int> CountCurrentlyHeldReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Reservations
            .Where(r => r.LotId == lotId &&
                        r.VehicleGroup == vehicleGroup &&
                        (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed) &&
                        r.UsedAt == null &&
                        r.CancelledAt == null)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(r => r.IsActive(evaluationTime) && r.GetReservationWindow().Contains(evaluationTime))
            .Count();
    }

    public void Add(Reservation reservation)
    {
        context.Reservations.Add(reservation);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
