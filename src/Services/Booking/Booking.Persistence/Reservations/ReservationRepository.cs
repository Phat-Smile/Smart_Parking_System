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
        // Cho phép xe đến muộn tối đa 15 phút (NoShowGracePeriodMinutes) + 1 phút dung sai kỹ thuật (TechnicalToleranceMinutes)
        return candidates.FirstOrDefault(r =>
            utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
            utcArrival < Reservation.GetNoShowCutoff(r.AppointmentTime));
    }

    public async Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(
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
                        r.Status == ReservationStatus.Cancelled &&
                        r.CancelReason == CancelReason.NoSuitableSlot)
            .OrderByDescending(r => r.CancelledAt)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(r =>
            utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
            utcArrival < Reservation.GetNoShowCutoff(r.AppointmentTime));
    }

    public void Add(Reservation reservation) =>
        dbContext.Reservations.Add(reservation);

    public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken) =>
        dbContext.Entry(reservation).ReloadAsync(cancellationToken);

    public void DiscardPendingChanges() =>
        dbContext.ChangeTracker.Clear();

    public async Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var oneMinuteAgo = now.AddMinutes(-1);
        var twentyFourHoursAgo = now.AddHours(-24);

        return await dbContext.Reservations
            .Where(r => (r.PrepaidAmount > 0m &&
                         r.ConfirmedAt == null &&
                         r.PrepaidOutcome == null &&
                         r.TempHoldExpiresAt != null &&
                         r.TempHoldExpiresAt <= oneMinuteAgo &&
                         r.TempHoldExpiresAt > twentyFourHoursAgo &&
                         (r.Status == ReservationStatus.TemporaryHold ||
                          r.Status == ReservationStatus.Expired ||
                          r.Status == ReservationStatus.Cancelled))
                        ||
                        (r.Status == ReservationStatus.Cancelled &&
                         (r.CancelReason == CancelReason.CustomerCancelled ||
                          r.CancelReason == CancelReason.NoSuitableSlot) &&
                         r.ConfirmedAt != null &&
                         r.PrepaidAmount > 0m &&
                         r.PrepaidOutcome == null))
            .OrderBy(r => r.TempHoldExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var graceThreshold = Reservation.GetNoShowCandidateThreshold(now);

        return await dbContext.Reservations
            .Where(r => r.Status == ReservationStatus.Confirmed &&
                        r.AppointmentTime <= graceThreshold)
            .OrderBy(r => r.AppointmentTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> ListArrivalReminderCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var minTime = now.AddMinutes(-20);
        var maxTime = now.AddMinutes(35);

        return await dbContext.Reservations
            .Where(r => r.Status == ReservationStatus.Confirmed &&
                        r.AppointmentTime >= minTime &&
                        r.AppointmentTime <= maxTime &&
                        (
                            (r.ArrivalReminderSentAtUtc == null && r.AppointmentTime <= now.AddMinutes(30)) ||
                            (r.NoShowWarningSentAtUtc == null && r.AppointmentTime <= now.AddMinutes(-10))
                        ))
            .OrderBy(r => r.AppointmentTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
