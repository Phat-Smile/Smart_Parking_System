using Microsoft.EntityFrameworkCore;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Persistence.CustomerNotifications;

internal sealed class CustomerNotificationStore(NotificationDbContext db) : ICustomerNotificationStore
{
    public async Task<CustomerNotificationPage> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        var own = db.CustomerNotifications.AsNoTracking().Where(x => x.UserId == userId);
        var total = await own.CountAsync(ct);
        var unread = await own.CountAsync(x => x.ReadAtUtc == null, ct);
        var query = unreadOnly ? own.Where(x => x.ReadAtUtc == null) : own;
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CustomerNotificationItem(x.Id, x.Type, x.Title, x.Description,
                x.Href, x.CreatedAtUtc, x.ReadAtUtc)).ToListAsync(ct);
        return new CustomerNotificationPage(items, total, unread, page, pageSize);
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTimeOffset now, CancellationToken ct)
    {
        var changed = await db.CustomerNotifications
            .Where(x => x.UserId == userId && x.Id == notificationId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReadAtUtc, now), ct);
        if (changed != 0) return true;
        return await db.CustomerNotifications.AnyAsync(x => x.UserId == userId && x.Id == notificationId, ct);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.CustomerNotifications.Where(x => x.UserId == userId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReadAtUtc, now), ct);

    public async Task AddIfAbsentAsync(Guid userId, string sourceEventKey, string type, string title,
        string description, string href, DateTimeOffset createdAtUtc, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification.customer_notifications
                (id, user_id, source_event_key, type, title, description, href, created_at_utc)
            VALUES ({id}, {userId}, {sourceEventKey}, {type}, {title}, {description}, {href}, {createdAtUtc})
            ON CONFLICT (source_event_key) DO NOTHING
            """, ct);
    }
}
