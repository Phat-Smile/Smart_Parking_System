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
    public string? LicensePlate { get; private set; }
    public VehicleDomainType VehicleType { get; private set; }
    public string? NickName { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsDefault { get; private set; }
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? MotorbikeKind { get; private set; }
    public int? Seats { get; private set; }
    public string? FuelType { get; private set; }
    public string? Color { get; private set; }
    public int? ManufacturingYear { get; private set; }
    public string? Size { get; private set; }
    public string? PhotoUrl { get; private set; }

    public static Vehicle Add(
        Guid id,
        Guid accountId,
        string? licensePlate,
        VehicleDomainType vehicleType,
        DateTimeOffset createdAtUtc,
        string? nickName = null,
        bool isDefault = false)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Vehicle Id must not be empty.", nameof(id));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        if (!Enum.IsDefined(vehicleType))
            throw new ArgumentException("Unknown VehicleType.", nameof(vehicleType));

        if (vehicleType == VehicleDomainType.Bicycle && !string.IsNullOrWhiteSpace(licensePlate))
            throw new ArgumentException("A bicycle does not use a license plate.", nameof(licensePlate));
        if (vehicleType != VehicleDomainType.Bicycle)
            ArgumentException.ThrowIfNullOrWhiteSpace(licensePlate);
        var plate = string.IsNullOrWhiteSpace(licensePlate) ? null : licensePlate.Trim().ToUpperInvariant();
        if (plate?.Length > MaxLicensePlateLength)
            throw new ArgumentException($"LicensePlate must not exceed {MaxLicensePlateLength} characters.", nameof(licensePlate));

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
            CreatedAtUtc = createdAtUtc,
            IsDefault = isDefault
        };
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void UnsetDefault()
    {
        IsDefault = false;
    }

    public void UpdateDetails(string? nickName, string? brand, string? model, string? motorbikeKind,
        int? seats, string? fuelType, string? color, int? manufacturingYear, string? size, string? photoUrl)
    {
        NickName = TrimOrNull(nickName);
        Brand = TrimOrNull(brand);
        Model = TrimOrNull(model);
        MotorbikeKind = TrimOrNull(motorbikeKind);
        Seats = seats;
        FuelType = TrimOrNull(fuelType);
        Color = TrimOrNull(color);
        ManufacturingYear = manufacturingYear;
        Size = TrimOrNull(size);
        PhotoUrl = TrimOrNull(photoUrl);
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
