using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationRepository(BookingDbContext dbContext) : IReservationRepository
{
    public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reservations.FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Reservations.AsNoTracking().Where(reservation => reservation.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(reservation => reservation.Status == status.Value);
        }

        return await query.OrderByDescending(reservation => reservation.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken)
    {
        return await dbContext.Reservations
            .Where(reservation => reservation.LotId == lotId &&
                (reservation.Status == ReservationStatus.TemporaryHold || reservation.Status == ReservationStatus.Confirmed))
            .ToListAsync(cancellationToken);
    }

    public async Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
        Guid lotId,
        string plateNumber,
        DateTimeOffset arrivalTime,
        CancellationToken cancellationToken)
    {
        var normalizedPlate = LicensePlateNormalizer.Normalize(plateNumber);
        var utcArrival = arrivalTime.ToUniversalTime();

        var candidates = await dbContext.Reservations
            .Where(r => r.LotId == lotId &&
                        r.PlateNumber == normalizedPlate &&
                        r.Status == ReservationStatus.Confirmed)
            .ToListAsync(cancellationToken);

        // Khung giờ đón xe hợp lệ (MF-08):
        // Cho phép xe đến sớm tối đa 15 phút so với WindowStartAt
        // Cho phép xe đến muộn tối đa 15 phút (NoShowGracePeriodMinutes) so với AppointmentTime
        return candidates.FirstOrDefault(r =>
            utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
            utcArrival <= r.AppointmentTime.AddMinutes(Reservation.NoShowGracePeriodMinutes));
    }

    public void Add(Reservation reservation) =>
        dbContext.Reservations.Add(reservation);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
