using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

public interface IManageParkingLayoutRepository
{
    Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken);
    Task<bool> IsOwnerAsync(Guid lotId, Guid actorId, CancellationToken cancellationToken);
    Task<ParkingLotLayout> ReadAsync(Guid lotId, CancellationToken cancellationToken);
    Task<Error?> SaveAsync(Guid lotId, Guid actorId, int expectedVersion, IReadOnlyList<LayoutFloorInput> floors, CancellationToken cancellationToken);
}
