using System.Text.Json;
using ParkingSystem.Parking.Application.Common.Auditing;

namespace ParkingSystem.Parking.Persistence.Auditing;

// Adds the entry to the same DbContext as the business change, so both are saved in one transaction.
internal sealed class AuditLog(ParkingDbContext dbContext) : IAuditLog
{
    public void Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = entry.OccurredAt,
            ActorUserId = entry.ActorUserId,
            Action = entry.Action,
            LotId = entry.LotId,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            Reason = entry.Reason,
            Details = entry.Details is null ? null : JsonSerializer.Serialize(entry.Details)
        });
    }
}
