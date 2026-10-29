using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Repositories;

internal sealed class ReservationRatioPolicyRepository(ParkingDbContext dbContext) : IReservationRatioPolicyRepository
{
    public async Task<ReservationRatioPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ReservationRatioPolicies
            .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);
    }

    public async Task AddAsync(ReservationRatioPolicy policy, CancellationToken cancellationToken = default)
    {
        await dbContext.ReservationRatioPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }

    public async Task<ReservationRatioPolicy> UpsertAsync(Guid lotId, int ratioPercent, CancellationToken cancellationToken = default)
    {
        var existingPolicy = await dbContext.ReservationRatioPolicies
            .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);

        if (existingPolicy != null)
        {
            existingPolicy.Update(ratioPercent);
            await dbContext.SaveChangesAsync(cancellationToken);
            return existingPolicy;
        }

        var newPolicy = ReservationRatioPolicy.Create(lotId, ratioPercent);
        await dbContext.ReservationRatioPolicies.AddAsync(newPolicy, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return newPolicy;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentExisting = await dbContext.ReservationRatioPolicies
                .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);

            if (concurrentExisting != null)
            {
                concurrentExisting.Update(ratioPercent);
                await dbContext.SaveChangesAsync(cancellationToken);
                return concurrentExisting;
            }

            throw;
        }
    }
}
