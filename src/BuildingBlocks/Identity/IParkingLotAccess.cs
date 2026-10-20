namespace ParkingSystem.BuildingBlocks.Identity;

// TECH-12: role trong token chưa đủ, còn phải được gắn với đúng bãi. Mỗi service tự cài cách tra (Parking tra bảng phân công).
public interface IParkingLotAccess
{
    Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken);
}
