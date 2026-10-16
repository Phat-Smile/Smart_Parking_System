namespace ParkingSystem.Parking.Domain.ParkingLots;

public enum ParkingLotMemberRole
{
    Owner = 1,
    Staff = 2
}

// TECH-12: một tài khoản (Identity) được gắn với một bãi; mỗi cặp bãi - tài khoản chỉ có một vai trò.
public sealed class ParkingLotAssignment
{
    private ParkingLotAssignment()
    {
    }

    public Guid LotId { get; private set; }
    public Guid AccountId { get; private set; }
    public ParkingLotMemberRole Role { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }

    public static ParkingLotAssignment Create(Guid lotId, Guid accountId, ParkingLotMemberRole role, Guid assignedBy, DateTimeOffset assignedAtUtc)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("LotId must not be empty.", nameof(lotId));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));

        return new ParkingLotAssignment
        {
            LotId = lotId,
            AccountId = accountId,
            Role = role,
            AssignedBy = assignedBy,
            AssignedAtUtc = assignedAtUtc
        };
    }

    // Trả về false khi vai trò không đổi, để gọi lại không ghi đè người gán và thời điểm gán.
    public bool ChangeRole(ParkingLotMemberRole role, Guid assignedBy, DateTimeOffset assignedAtUtc)
    {
        if (Role == role)
            return false;

        Role = role;
        AssignedBy = assignedBy;
        AssignedAtUtc = assignedAtUtc;
        return true;
    }
}
