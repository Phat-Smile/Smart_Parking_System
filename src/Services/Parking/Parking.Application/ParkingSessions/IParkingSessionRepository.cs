using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions;

public interface IParkingSessionRepository
{
    /// <summary>True when the plate already has an Active or Paid session in the lot.</summary>
    Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken);

    /// <summary>Returns the tracked session, or null when it does not exist.</summary>
    Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Active or Paid sessions linked to the account, newest entry first (read only).</summary>
    Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The Active or Paid session that owns the QR token, or null; a closed session's QR is invalid (read only).</summary>
    Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken);

    /// <summary>One page of the lot's sessions, newest entry first, of one status or of all when null, with the total count (read only).</summary>
    Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
        Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken);

    /// <summary>The Active or Paid session of the plate in the lot, or null (read only). The open-plate unique index allows at most one.</summary>
    Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken);

    void Add(ParkingSession session);

    /// <summary>Saves pending changes. Returns false when the plate already has an open session in the lot (unique index).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
