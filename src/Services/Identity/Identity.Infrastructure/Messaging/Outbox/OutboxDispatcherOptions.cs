namespace ParkingSystem.Identity.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "Outbox";
    public bool Enabled { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int BatchSize { get; set; } = 50;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    // One-time secrets must not stay in the Outbox table after delivery (OTP is valid for 5 minutes).
    public string[] RedactedPayloadKeys { get; set; } = ["OtpCode"];
}

