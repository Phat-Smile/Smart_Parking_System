namespace ParkingSystem.Identity.Domain.Vehicles;

public sealed class Vehicle
{
    public const int MaxLicensePlateLength = 20;
    public const int MaxNickNameLength = 50;

    private Vehicle()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string LicensePlate { get; private set; } = null!;
    public VehicleDomainType VehicleType { get; private set; }
    public string? NickName { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Vehicle Add(
        Guid id,
        Guid accountId,
        string licensePlate,
        VehicleDomainType vehicleType,
        DateTimeOffset createdAtUtc,
        string? nickName = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Vehicle Id must not be empty.", nameof(id));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(licensePlate);

        var plate = licensePlate.Trim().ToUpperInvariant();
        if (plate.Length > MaxLicensePlateLength)
            throw new ArgumentException($"LicensePlate must not exceed {MaxLicensePlateLength} characters.", nameof(licensePlate));
        if (!Enum.IsDefined(vehicleType))
            throw new ArgumentException("Unknown VehicleType.", nameof(vehicleType));

        var trimmedNick = nickName?.Trim();
        if (trimmedNick?.Length > MaxNickNameLength)
            throw new ArgumentException($"NickName must not exceed {MaxNickNameLength} characters.", nameof(nickName));

        return new Vehicle
        {
            Id = id,
            AccountId = accountId,
            LicensePlate = plate,
            VehicleType = vehicleType,
            NickName = string.IsNullOrEmpty(trimmedNick) ? null : trimmedNick,
            CreatedAtUtc = createdAtUtc
        };
    }
}
