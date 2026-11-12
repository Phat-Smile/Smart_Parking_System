using System;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IReservationAvailabilityPolicyRepository
{
    Task<ReservationAvailabilityPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default);
    Task AddAsync(ReservationAvailabilityPolicy policy, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ReservationAvailabilityPolicy> UpsertAsync(Guid lotId, bool reservationEnabled, CancellationToken cancellationToken = default);
}
