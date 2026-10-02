namespace ParkingSystem.BuildingBlocks.Correlation;

public sealed record CorrelationContext(Guid CorrelationId, Guid? CausationId = null);
