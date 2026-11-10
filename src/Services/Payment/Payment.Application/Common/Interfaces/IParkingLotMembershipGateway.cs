using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IParkingLotMembershipGateway
{
    Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
        Guid lotId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}

public sealed record ParkingLotMembershipInfo(bool IsMember, string Role);
