namespace ParkingSystem.AspNetCore.Security;

public sealed class DeviceKeyOptions
{
    public const string SectionName = "DeviceAuth";

    // Shared secret of the IoT slot sensors. Empty means the sensor route stays closed.
    public string SensorKey { get; set; } = string.Empty;
}
