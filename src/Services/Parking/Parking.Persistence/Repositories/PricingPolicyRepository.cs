using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Repositories;

public class PricingPolicyRepository : IPricingPolicyRepository
{
    private readonly ParkingDbContext _context;

    public PricingPolicyRepository(ParkingDbContext context)
    {
        _context = context;
    }

    // Lay bang gia moi nhat theo ma bai va loai xe
    public async Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken)
    {
        return await _context.PricingPolicies
            .Where(p => p.LotId == lotId && p.VehicleType == vehicleType)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var atUtc = at.UtcDateTime;
        return await _context.PricingPolicies.AsNoTracking()
            .Where(p => p.LotId == lotId && p.VehicleType == vehicleType && p.EffectiveFromUtc <= atUtc)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Them bang gia moi vao DbContext
    public async Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken)
    {
        await _context.PricingPolicies.AddAsync(policy, cancellationToken);
    }

    // Luu thay doi va bat loi xung dot phien ban
    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
