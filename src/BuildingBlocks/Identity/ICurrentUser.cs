namespace ParkingSystem.BuildingBlocks.Identity;

public interface ICurrentUser
{
    string? SubjectId { get; }
    // Default keeps existing implementers (tests, other hosts) compiling; the HTTP implementation reads the "sid" claim.
    string? SessionId => null;
    bool IsAuthenticated { get; }
}
