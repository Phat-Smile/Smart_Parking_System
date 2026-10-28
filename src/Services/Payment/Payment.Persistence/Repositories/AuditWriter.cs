using System.Text.Json;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

public sealed class AuditWriter(
    PaymentDbContext dbContext,
    ICurrentActor currentActor,
    IClock clock) : IAuditWriter
{
    public Task WriteAsync(AuditEntryInput entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.Reason))
            throw new ArgumentException(AuditLogErrors.ReasonRequired.Message, nameof(entry.Reason));

        string? detailsJson = null;
        if (entry.Details is { Count: > 0 })
        {
            // Kiểm tra từng key để chặn rò rỉ password hoặc token
            foreach (var key in entry.Details.Keys)
            {
                if (AuditDetailsSecurity.ContainsForbiddenKey(key))
                {
                    throw new InvalidOperationException(
                        $"{AuditLogErrors.DetailsKeyNotAllowed.Message}: Key '{key}' is forbidden.");
                }
            }

            detailsJson = JsonSerializer.Serialize(entry.Details);
        }

        var nowUtc = clock.UtcNow.UtcDateTime;

        var auditLog = AuditLog.CreateFromPayment(
            id: Guid.NewGuid(),
            occurredAtUtc: nowUtc,
            recordedAtUtc: nowUtc,
            actionType: entry.ActionType,
            actorId: currentActor.ActorId,
            actorEmail: currentActor.Email,
            actorRole: currentActor.Role,
            parkingLotId: entry.ParkingLotId,
            targetType: entry.TargetType,
            targetId: entry.TargetId,
            reason: entry.Reason,
            ipAddress: currentActor.IpAddress,
            details: detailsJson);

        dbContext.Set<AuditLog>().Add(auditLog);
        return Task.CompletedTask;
    }
}
