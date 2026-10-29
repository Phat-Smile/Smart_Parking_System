using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Repositories;

internal sealed class ReservationAvailabilityPolicyRepository(ParkingDbContext dbContext) : IReservationAvailabilityPolicyRepository
{
    private const string UniqueIndexName = "ux_reservation_availability_policies_lot_id";

    public async Task<ReservationAvailabilityPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ReservationAvailabilityPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);
    }

    public async Task AddAsync(ReservationAvailabilityPolicy policy, CancellationToken cancellationToken = default)
    {
        await dbContext.ReservationAvailabilityPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }

    public async Task<ReservationAvailabilityPolicy> UpsertAsync(Guid lotId, bool reservationEnabled, CancellationToken cancellationToken = default)
    {
        var existingPolicy = await dbContext.ReservationAvailabilityPolicies
            .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);

        if (existingPolicy != null)
        {
            existingPolicy.Update(reservationEnabled);
            await dbContext.SaveChangesAsync(cancellationToken);
            return existingPolicy;
        }

        var newPolicy = ReservationAvailabilityPolicy.Create(lotId, reservationEnabled);
        await dbContext.ReservationAvailabilityPolicies.AddAsync(newPolicy, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return newPolicy;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(newPolicy).State = EntityState.Detached;
            var concurrentExisting = await dbContext.ReservationAvailabilityPolicies
                .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);

            if (concurrentExisting != null)
            {
                concurrentExisting.Update(reservationEnabled);
                await dbContext.SaveChangesAsync(cancellationToken);
                return concurrentExisting;
            }

            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState == "23505" &&
                   (pgEx.ConstraintName == UniqueIndexName ||
                    pgEx.ConstraintName?.Contains("reservation_availability_policies", StringComparison.OrdinalIgnoreCase) == true);
        }

        return false;
    }
}
