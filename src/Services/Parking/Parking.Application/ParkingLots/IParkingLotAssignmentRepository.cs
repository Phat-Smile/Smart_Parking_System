using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotAssignmentRepository
{
    // Mọi trạng thái duyệt: chủ bãi cần được gắn cả khi bãi còn chờ duyệt.
    Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken);

    Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken);

    void Add(ParkingLotAssignment assignment);

    void Remove(ParkingLotAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
