namespace ParkingSystem.Notification.Persistence.CustomerNotifications;

public sealed class CustomerNotification
{
    private CustomerNotification() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string SourceEventKey { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Href { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static CustomerNotification Create(Guid userId, string sourceEventKey, string type, string title,
        string description, string href, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, SourceEventKey = sourceEventKey, Type = type,
        Title = title, Description = description, Href = href, CreatedAtUtc = createdAtUtc
    };
}
