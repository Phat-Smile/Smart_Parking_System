using System;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public interface IAuditWriter
{
    void Log(Guid actorId, string action, string targetId, string details);
}
