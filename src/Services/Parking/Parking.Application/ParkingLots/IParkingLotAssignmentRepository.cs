using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotAssignmentRepository
{
    // Mọi trạng thái duyệt: chủ bãi cần được gắn cả khi bãi còn chờ duyệt.
    Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken);

    Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken);

    // Các bãi đã duyệt mà tài khoản được gán (Owner hoặc Staff), sắp theo tên (chỉ đọc).
    Task<IReadOnlyList<StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken);

    // Các tài khoản được gán vào bãi, gán trước đứng trước (chỉ đọc).
    Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken);

    void Add(ParkingLotAssignment assignment);

    void Remove(ParkingLotAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
