using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IParkingDbContext
{
    DbSet<PricingPolicy> PricingPolicies { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
