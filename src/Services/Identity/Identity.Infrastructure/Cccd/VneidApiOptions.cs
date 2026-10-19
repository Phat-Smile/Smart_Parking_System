namespace ParkingSystem.Identity.Infrastructure.Cccd;

public sealed class VneidApiOptions
{
    public const string SectionName = "VneidApi";
    public const string StubProvider = "Stub";
    public const string HttpProvider = "Http";

    public string Provider { get; set; } = StubProvider;
    public string BaseUrl { get; set; } = string.Empty;
}
