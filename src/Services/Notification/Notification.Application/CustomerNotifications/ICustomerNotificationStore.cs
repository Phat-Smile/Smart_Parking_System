namespace ParkingSystem.Notification.Application.CustomerNotifications;

public sealed record CustomerNotificationItem(Guid Id, string Type, string Title, string Description,
    string Href, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);

public sealed record CustomerNotificationPage(IReadOnlyList<CustomerNotificationItem> Items,
    int TotalCount, int UnreadCount, int Page, int PageSize);

public interface ICustomerNotificationStore
{
    Task<CustomerNotificationPage> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTimeOffset now, CancellationToken ct);
    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    Task AddIfAbsentAsync(Guid userId, string sourceEventKey, string type, string title,
        string description, string href, DateTimeOffset createdAtUtc, CancellationToken ct);
}
