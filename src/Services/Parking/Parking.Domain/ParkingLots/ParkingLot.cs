namespace ParkingSystem.Parking.Domain.ParkingLots;

public sealed class ParkingLot
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 500;

    private ParkingLot()
    {
    }

    public Guid Id { get; private set; }
    // Nullable for pre-existing catalog / TECH-02 seeded lots. All newly registered lots have an owner.
    public Guid? OwnerUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public ParkingLotType Type { get; private set; }
    public ParkingLotApprovalStatus ApprovalStatus { get; private set; }
    public bool SupportsElectricVehicles { get; private set; }
    public bool SupportsLargeVehicles { get; private set; }
    public bool HasEvCharging { get; private set; }
    public bool IsCovered { get; private set; }
    public bool HasCctv { get; private set; }

    public static ParkingLot Register(
        Guid id,
        Guid ownerUserId,
        string name,
        string address,
        decimal latitude,
        decimal longitude,
        ParkingLotType type)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Parking lot ID is required.", nameof(id));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("Owner user ID is required.", nameof(ownerUserId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
            throw new ArgumentException($"Name must contain 1-{NameMaxLength} characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length > AddressMaxLength)
            throw new ArgumentException($"Address must contain 1-{AddressMaxLength} characters.", nameof(address));
        if (latitude is < -90m or > 90m)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180m or > 180m)
            throw new ArgumentOutOfRangeException(nameof(longitude));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        return new ParkingLot
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = name.Trim(),
            Address = address.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            Type = type,
            ApprovalStatus = ParkingLotApprovalStatus.Pending
        };
    }

    public const int RejectionReasonMaxLength = 500;
    public string? RejectionReason { get; private set; }

    // Only an unreviewed lot may be approved or rejected. Re-review is a separate owner workflow.
    public bool Approve()
    {
        if (ApprovalStatus != ParkingLotApprovalStatus.Pending)
            return false;

        ApprovalStatus = ParkingLotApprovalStatus.Approved;
        RejectionReason = null;
        return true;
    }

    public bool Reject(string reason)
    {
        if (ApprovalStatus != ParkingLotApprovalStatus.Pending)
            return false;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > RejectionReasonMaxLength)
            throw new ArgumentException("A valid rejection reason is required.", nameof(reason));

        ApprovalStatus = ParkingLotApprovalStatus.Rejected;
        RejectionReason = reason.Trim();
        return true;
    }

    public string? OpeningHoursJson { get; private set; }
    public string? PublicInfoJson { get; private set; }

    public void SetPublicInfo(ParkingLotPublicInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        PublicInfoJson = System.Text.Json.JsonSerializer.Serialize(profile,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }

    public void SetFeatures(ParkingLotFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (ApprovalStatus != ParkingLotApprovalStatus.Pending)
            throw new InvalidOperationException("Features can only change before lot approval.");
        SupportsElectricVehicles = features.SupportsElectricVehicles;
        SupportsLargeVehicles = features.SupportsLargeVehicles;
        HasEvCharging = features.HasEvCharging;
        IsCovered = features.IsCovered;
        HasCctv = features.HasCctv;
    }

    public void SetOpeningHours(string openingHoursJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(openingHoursJson);
        OpeningHoursJson = openingHoursJson;
    }
}
