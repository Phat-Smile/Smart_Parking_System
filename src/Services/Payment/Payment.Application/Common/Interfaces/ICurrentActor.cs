namespace ParkingSystem.Payment.Application.Common.Interfaces;

/// <summary>
/// Cung cấp thông tin người thực hiện thao tác hiện tại để chụp snapshot.
/// </summary>
public interface ICurrentActor
{
    Guid? ActorId { get; }
    string? Email { get; }
    string Role { get; }
    string? IpAddress { get; }
}
