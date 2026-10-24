namespace ParkingSystem.Parking.Domain.ParkingLayout;

// This lifecycle is separate from ParkingLotApprovalStatus and PR #82.
public enum ParkingLayoutChangeStatus { Pending, Approved, Rejected }

/// <summary>
/// Proposed rendering/layout snapshot. A Pending request must not modify the
/// operational Floors, Zones, ParkingSlots or active LayoutDocument.
/// </summary>
public sealed class ParkingLayoutChangeRequest
{
    private ParkingLayoutChangeRequest() { }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public int ExpectedVersion { get; private set; }
    public string ProposedLayoutJson { get; private set; } = "[]";
    public ParkingLayoutChangeStatus Status { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    public static ParkingLayoutChangeRequest Submit(
        Guid id, Guid lotId, Guid requestedBy, int expectedVersion,
        string proposedLayoutJson, DateTimeOffset requestedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Request ID is required.", nameof(id));
        if (lotId == Guid.Empty) throw new ArgumentException("Lot ID is required.", nameof(lotId));
        if (requestedBy == Guid.Empty) throw new ArgumentException("Requester ID is required.", nameof(requestedBy));
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedLayoutJson);
        return new ParkingLayoutChangeRequest
        {
            Id = id,
            LotId = lotId,
            RequestedBy = requestedBy,
            ExpectedVersion = expectedVersion,
            ProposedLayoutJson = proposedLayoutJson,
            Status = ParkingLayoutChangeStatus.Pending,
            RequestedAtUtc = requestedAtUtc
        };
    }

    public bool Approve(Guid adminId, DateTimeOffset reviewedAtUtc)
    {
        if (adminId == Guid.Empty) throw new ArgumentException("Admin ID is required.", nameof(adminId));
        if (Status != ParkingLayoutChangeStatus.Pending) return false;
        Status = ParkingLayoutChangeStatus.Approved;
        ReviewedBy = adminId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = null;
        return true;
    }

    public bool Reject(Guid adminId, string reason, DateTimeOffset reviewedAtUtc)
    {
        if (adminId == Guid.Empty) throw new ArgumentException("Admin ID is required.", nameof(adminId));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("A rejection reason of 1–500 characters is required.", nameof(reason));
        if (Status != ParkingLayoutChangeStatus.Pending) return false;
        Status = ParkingLayoutChangeStatus.Rejected;
        ReviewedBy = adminId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = reason.Trim();
        return true;
    }
}
