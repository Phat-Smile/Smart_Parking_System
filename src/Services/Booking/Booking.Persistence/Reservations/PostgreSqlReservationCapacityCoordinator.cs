using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

/// <summary>
/// Implements atomic capacity concurrency control using PostgreSQL row-level locks
/// (SELECT ... FOR UPDATE on booking.reservation_capacity) inside a local database transaction (BR-02.c, US-RES-005).
/// </summary>
public sealed class PostgreSqlReservationCapacityCoordinator(BookingDbContext context) : IReservationCapacityCoordinator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LocalSemaphores = new();

    public async Task<TResult> ExecuteUnderCapacityLockAsync<TResult>(
        Guid lotId,
        VehicleGroup vehicleGroup,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var key = $"{lotId}:{vehicleGroup}";
        var semaphore = LocalSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var isRelational = context.Database.IsRelational();
            if (!isRelational)
            {
                return await action(cancellationToken);
            }

            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            // If connected to PostgreSQL, execute row-level lock on reservation_capacity
            if (context.Database.IsNpgsql())
            {
                await AcquirePostgreSqlRowLockAsync(lotId, vehicleGroup, cancellationToken);
            }

            var result = await action(cancellationToken);

            // If result is a Result<T> with IsFailure, rollback
            if (result is not null)
            {
                var isFailureProp = result.GetType().GetProperty("IsFailure");
                if (isFailureProp is not null && isFailureProp.GetValue(result) is true)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task AcquirePostgreSqlRowLockAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken)
    {
        var groupString = vehicleGroup.ToString();
        var now = DateTimeOffset.UtcNow;

        // Ensure row exists for the lot and vehicle group
        var insertSql = $"""
            INSERT INTO {BookingDbContext.DefaultSchema}.reservation_capacity (lot_id, vehicle_group, updated_at)
            VALUES (@lotId, @vehicleGroup, @now)
            ON CONFLICT (lot_id, vehicle_group) DO NOTHING;
            """;

        await context.Database.ExecuteSqlRawAsync(
            insertSql,
            [
                new NpgsqlParameter("lotId", lotId),
                new NpgsqlParameter("vehicleGroup", groupString),
                new NpgsqlParameter("now", now)
            ],
            cancellationToken);

        // Acquire exclusive row lock
        var lockSql = $"""
            SELECT lot_id, vehicle_group, updated_at
            FROM {BookingDbContext.DefaultSchema}.reservation_capacity
            WHERE lot_id = @lotId AND vehicle_group = @vehicleGroup
            FOR UPDATE;
            """;

        await context.Database.ExecuteSqlRawAsync(
            lockSql,
            [
                new NpgsqlParameter("lotId", lotId),
                new NpgsqlParameter("vehicleGroup", groupString)
            ],
            cancellationToken);
    }

    public async Task RecordCapacityUpdatedAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var capacity = await context.ReservationCapacities
            .FirstOrDefaultAsync(c => c.LotId == lotId && c.VehicleGroup == vehicleGroup, cancellationToken);

        if (capacity is null)
        {
            capacity = ReservationCapacity.Create(lotId, vehicleGroup, updatedAt);
            context.ReservationCapacities.Add(capacity);
        }
        else
        {
            capacity.Touch(updatedAt);
        }
    }
}
