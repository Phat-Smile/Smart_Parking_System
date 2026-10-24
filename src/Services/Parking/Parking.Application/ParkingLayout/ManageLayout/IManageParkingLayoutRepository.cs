using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

public interface IManageParkingLayoutRepository
{
    Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken);
    Task<bool> IsOwnerAsync(Guid lotId, Guid actorId, CancellationToken cancellationToken);
    Task<ParkingLotLayout> ReadAsync(Guid lotId, CancellationToken cancellationToken);
    Task<SaveLayoutOutcome> SaveAsync(Guid lotId, Guid actorId, int expectedVersion, IReadOnlyList<LayoutFloorInput> floors, CancellationToken cancellationToken);
    Task<IReadOnlyList<LayoutReviewRequest>> ListPendingReviewsAsync(CancellationToken cancellationToken);
    Task<ReviewLayoutOutcome> ReviewAsync(Guid lotId, Guid requestId, Guid adminId, bool approve, string? reason, CancellationToken cancellationToken);
}
