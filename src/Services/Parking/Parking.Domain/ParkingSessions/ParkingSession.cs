namespace ParkingSystem.Parking.Domain.ParkingSessions;

public sealed class ParkingSession
{
    // Placeholder: the plate format is not specified in Context yet.
    public const int PlateNumberMaxLength = 20;

    // BR-11.b: 32 random bytes encode to 43 Base64Url characters.
    public const int QrTokenByteLength = 32;
    public const int QrTokenMaxLength = 64;

    // Statuses that still hold the plate in the lot (BR-11.a).
    public static readonly IReadOnlyList<ParkingSessionStatus> OpenStatuses =
        [ParkingSessionStatus.Active, ParkingSessionStatus.Paid];

    private ParkingSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public string PlateNumber { get; private set; } = null!;
    public VehicleType VehicleType { get; private set; }
    public ParkingSessionStatus Status { get; private set; }
    public DateTimeOffset EnteredAt { get; private set; }
    public string QrToken { get; private set; } = null!;

    // Ids owned by other services, stored without foreign keys. Null when the plate has no account (BR-05.e).
    public Guid? UserId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? BookingId { get; private set; }

    public static ParkingSession Open(
        Guid id,
        Guid lotId,
        string plateNumber,
        VehicleType vehicleType,
        DateTimeOffset enteredAt,
        string qrToken,
        Guid? userId,
        Guid? vehicleId)
    {
        return new ParkingSession
        {
            Id = id,
            LotId = lotId,
            PlateNumber = NormalizePlate(plateNumber),
            VehicleType = vehicleType,
            Status = ParkingSessionStatus.Active, // BR-11.a: a new session is always Active.
            EnteredAt = enteredAt,
            QrToken = qrToken,
            UserId = userId,
            VehicleId = vehicleId
        };
    }

    public static string NormalizePlate(string plateNumber) => plateNumber.Trim().ToUpperInvariant();
}
