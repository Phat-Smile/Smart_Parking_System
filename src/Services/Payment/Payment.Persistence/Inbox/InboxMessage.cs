namespace ParkingSystem.Payment.Persistence.Inbox;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string ConsumerKey { get; set; } = null!;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
}