using System;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Infrastructure.Auditing;

public class AuditWriter : IAuditWriter
{
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(ILogger<AuditWriter> logger)
    {
        _logger = logger;
    }

    public void Log(Guid actorId, string action, string targetId, string details)
    {
        _logger.LogInformation("AUDIT [{Action}] by Actor {ActorId} on Target {TargetId}: {Details}", 
            action, actorId, targetId, details);
    }
}
