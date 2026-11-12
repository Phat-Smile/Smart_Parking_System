using System;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IReservationDepositPolicyRepository
{
    Task<ReservationDepositPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default);
    Task AddAsync(ReservationDepositPolicy policy, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
