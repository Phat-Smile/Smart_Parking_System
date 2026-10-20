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

    void Add(ParkingSession session);

    /// <summary>Saves pending changes. Returns false when the plate already has an open session in the lot (unique index).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
