namespace ParkingSystem.BuildingBlocks.Identity;

public interface ICurrentUser
{
    string? SubjectId { get; }
    bool IsAuthenticated { get; }
}
