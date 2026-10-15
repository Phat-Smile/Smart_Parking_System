namespace ParkingSystem.BuildingBlocks.Identity;

public interface ICurrentUser
{
    string? SubjectId { get; }
    string? SessionId { get; }
    bool IsAuthenticated { get; }
}
