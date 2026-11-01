using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Persistence.Reservations;

/// <summary>
/// Persistence coordinator implementing atomic reservation hold and capacity guard logic.
/// Locks the corresponding row (lot_id, vehicle_group) in booking.reservation_capacity
/// within a single ReadCommitted transaction, counts active overlapping holds, and inserts the new hold.
/// </summary>
public sealed class ReservationCapacityCoordinator(
    BookingDbContext dbContext,
    IClock clock,
    ILogger<ReservationCapacityCoordinator> logger) : IReservationCapacityCoordinator
{
    public async Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
        Reservation reservation,
        int ceiling,
        int? availableSlotCount,
        TimeSpan lockTimeout,
        int maxActiveReservationsPerUser = 3,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var now = clock.UtcNow;

                // Step 1: Ensure the coordinator row exists (ON CONFLICT DO NOTHING)
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO booking.reservation_capacity (lot_id, vehicle_group, updated_at)
                    VALUES ({0}, {1}, {2})
                    ON CONFLICT (lot_id, vehicle_group) DO NOTHING;
                    """,
                    [reservation.LotId, reservation.VehicleGroup, now],
                    cancellationToken);

                // Step 2: Set local lock timeout for this transaction to prevent indefinite blocking
                var timeoutMs = Math.Max(100, (int)lockTimeout.TotalMilliseconds);
#pragma warning disable EF1002 // timeoutMs is a sanitized integer from TimeSpan
                await dbContext.Database.ExecuteSqlRawAsync(
                    $"SET LOCAL lock_timeout = '{timeoutMs}ms';",
                    cancellationToken);
#pragma warning restore EF1002

                // Step 3: Concurrency protection for US-RES-010 (User limit & Plate overlap)
                // 3.1: Acquire transaction-level advisory lock on User (namespace 1)
                await dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(1, hashtext({0}::text));",
                    [reservation.UserId.ToString()],
                    cancellationToken);

                // 3.2: Acquire transaction-level advisory lock on Canonical Plate (namespace 2)
                var normalizedPlate = LicensePlateNormalizer.Normalize(reservation.PlateNumber);
                await dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(2, hashtext({0}::text));",
                    [normalizedPlate],
                    cancellationToken);

                // 3.3: Acquire exclusive row-level lock on (lot_id, vehicle_group)
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    SELECT 1 FROM booking.reservation_capacity
                    WHERE lot_id = {0} AND vehicle_group = {1}
                    FOR UPDATE;
                    """,
                    [reservation.LotId, reservation.VehicleGroup],
                    cancellationToken);

                // Step 4: Idempotency check - if reservation with same Id already exists, return it safely
                var existingReservation = await dbContext.Reservations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reservation.Id, cancellationToken);

                if (existingReservation is not null)
                {
                    logger.LogInformation("Reservation {ReservationId} already exists. Returning existing hold without consuming additional capacity.",
                        reservation.Id);
                    await transaction.CommitAsync(cancellationToken);
                    return Result<Reservation>.Success(existingReservation);
                }

                // Step 5 (US-RES-010 BR-1): Enforce max active reservations per user account across all lots
                var userActiveCount = await dbContext.Reservations
                    .Where(r => r.UserId == reservation.UserId &&
                                (r.Status == ReservationStatus.Confirmed ||
                                 (r.Status == ReservationStatus.TemporaryHold && r.TempHoldExpiresAt > now)))
                    .CountAsync(cancellationToken);

                if (userActiveCount >= maxActiveReservationsPerUser)
                {
                    logger.LogInformation(
                        "Reservation {ReservationId} rejected: user {UserId} active reservations limit ({Limit}) reached. Current active={ActiveCount}.",
                        reservation.Id, reservation.UserId, maxActiveReservationsPerUser, userActiveCount);

                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Reservation>.Failure(ReservationCapacityErrors.UserActiveReservationsLimitExceeded);
                }

                // Step 6 (US-RES-010 BR-2): Enforce no overlapping active reservations for the same plate number
                // Comparison uses canonical normalized plate rules (ignoring case, all whitespace including tabs/NBSP, dashes, dots)
                // PostgreSQL regexp_replace with 'g' flag ensures global replacement matching Identity's normalization policy.
                var hasPlateOverlap = await dbContext.Reservations
                    .FromSql($"""
                        SELECT *
                        FROM booking.reservations
                        WHERE (status = 'confirmed'::booking.reservation_status OR (status = 'temporary_hold'::booking.reservation_status AND temp_hold_expires_at > {now}))
                          AND window_start_at < {reservation.WindowEndAt}
                          AND window_end_at > {reservation.WindowStartAt}
                          AND (
                            plate_number = {normalizedPlate}
                            OR plate_number = {reservation.PlateNumber}
                            OR upper(regexp_replace(plate_number, '[[:space:]\s\-\.\u00A0]', '', 'g')) = {normalizedPlate}
                          )
                        """)
                    .AnyAsync(cancellationToken);

                if (hasPlateOverlap)
                {
                    logger.LogInformation(
                        "Reservation {ReservationId} rejected: plate {PlateNumber} already has an active overlapping reservation for window [{WindowStartAt}, {WindowEndAt}).",
                        reservation.Id, reservation.PlateNumber, reservation.WindowStartAt, reservation.WindowEndAt);

                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Reservation>.Failure(ReservationCapacityErrors.PlateReservationOverlap);
                }

                // Step 7: Condition (i) - Count active reservations overlapping with the new reservation's ceiling window
                // Active reservation = Confirmed OR (TemporaryHold with temp_hold_expires_at > now)
                // Half-open interval overlap = r.WindowStartAt < reservation.WindowEndAt && r.WindowEndAt > reservation.WindowStartAt
                var overlappingActiveCount = await dbContext.Reservations
                    .Where(r => r.LotId == reservation.LotId &&
                                r.VehicleGroup == reservation.VehicleGroup &&
                                (r.Status == ReservationStatus.Confirmed ||
                                 (r.Status == ReservationStatus.TemporaryHold && r.TempHoldExpiresAt > now)) &&
                                r.WindowStartAt < reservation.WindowEndAt &&
                                r.WindowEndAt > reservation.WindowStartAt)
                    .CountAsync(cancellationToken);

                if (overlappingActiveCount >= ceiling)
                {
                    logger.LogInformation(
                        "Reservation {ReservationId} rejected: ceiling exceeded for lot {LotId}. Active overlapping={Overlapping}, Ceiling={Ceiling}.",
                        reservation.Id, reservation.LotId, overlappingActiveCount, ceiling);

                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityExceeded);
                }

                // Step 8: Condition (ii) - If the reservation window has already started at 'now',
                // count current active holds against availableSlotCount
                if (availableSlotCount.HasValue)
                {
                    var currentActiveHolds = await dbContext.Reservations
                        .Where(r => r.LotId == reservation.LotId &&
                                    r.VehicleGroup == reservation.VehicleGroup &&
                                    (r.Status == ReservationStatus.Confirmed ||
                                     (r.Status == ReservationStatus.TemporaryHold && r.TempHoldExpiresAt > now)) &&
                                    r.WindowStartAt <= now &&
                                    r.WindowEndAt > now)
                        .CountAsync(cancellationToken);

                    if (currentActiveHolds >= availableSlotCount.Value)
                    {
                        logger.LogInformation(
                            "Reservation {ReservationId} rejected: no available slots for lot {LotId}. Active holds at now={ActiveHolds}, Available={Available}.",
                            reservation.Id, reservation.LotId, currentActiveHolds, availableSlotCount.Value);

                        await transaction.RollbackAsync(cancellationToken);
                        return Result<Reservation>.Failure(ReservationCapacityErrors.NoAvailableSlots);
                    }
                }

                // Step 9: Conditions met! Insert new reservation and touch capacity updated_at
                dbContext.Reservations.Add(reservation);
                await dbContext.SaveChangesAsync(cancellationToken);

                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE booking.reservation_capacity
                    SET updated_at = {0}
                    WHERE lot_id = {1} AND vehicle_group = {2};
                    """,
                    [now, reservation.LotId, reservation.VehicleGroup],
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Reservation {ReservationId} temporary hold successfully created for lot {LotId}.",
                    reservation.Id, reservation.LotId);

                return Result<Reservation>.Success(reservation);
            }
            catch (NpgsqlException ex) when (ex.SqlState is "55P03" or "57014")
            {
                logger.LogWarning(ex, "Lock acquisition timed out for lot {LotId} and vehicle group {VehicleGroup}.",
                    reservation.LotId, reservation.VehicleGroup);
                await transaction.RollbackAsync(cancellationToken);
                return Result<Reservation>.Failure(ReservationCapacityErrors.LockTimeout);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in atomic capacity coordinator for reservation {ReservationId}.",
                    reservation.Id);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
