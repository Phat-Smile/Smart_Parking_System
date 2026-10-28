using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Repositories;

internal sealed class ReservationDepositPolicyRepository(ParkingDbContext dbContext) : IReservationDepositPolicyRepository
{
    public async Task<ReservationDepositPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ReservationDepositPolicies
            .FirstOrDefaultAsync(p => p.LotId == lotId, cancellationToken);
    }

    public async Task AddAsync(ReservationDepositPolicy policy, CancellationToken cancellationToken = default)
    {
        await dbContext.ReservationDepositPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
