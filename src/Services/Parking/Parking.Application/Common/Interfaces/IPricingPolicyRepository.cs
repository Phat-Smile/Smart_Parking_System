using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IPricingPolicyRepository
{
    // Tra cuu bang gia moi nhat theo bai va loai xe
    Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken);

    // BR-09.a: the price list in force at a moment, from the version history; null when none had started yet.
    Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken);

    // Them bang gia moi vao kho du lieu
    Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken);

    // Luu cac thay doi vao co so du lieu
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
