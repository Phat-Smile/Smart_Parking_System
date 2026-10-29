namespace ParkingSystem.AspNetCore.Security;

public sealed class ServiceKeyOptions
{
    public const string SectionName = "ServiceAuth";

    public string ServiceKey { get; set; } = string.Empty;
}
