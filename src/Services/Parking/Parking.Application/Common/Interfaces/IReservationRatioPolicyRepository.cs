using System;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IReservationRatioPolicyRepository
{
    Task<ReservationRatioPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default);
    Task AddAsync(ReservationRatioPolicy policy, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ReservationRatioPolicy> UpsertAsync(Guid lotId, int ratioPercent, CancellationToken cancellationToken = default);
}
