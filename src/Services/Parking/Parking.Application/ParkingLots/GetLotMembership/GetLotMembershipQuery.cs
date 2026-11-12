using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLots.GetLotMembership;

public sealed record GetLotMembershipQuery(Guid LotId, Guid AccountId) : IRequest<Result<LotMembershipDto>>;

public sealed record LotMembershipDto(bool IsMember, string Role);

public static class GetLotMembershipErrors
{
    public static readonly Error InvalidArguments =
        new("parking.lot.membership.invalid_arguments", "LotId and AccountId must not be empty.");
}

public sealed class GetLotMembershipQueryHandler(IParkingLotAssignmentRepository assignments)
    : IRequestHandler<GetLotMembershipQuery, Result<LotMembershipDto>>
{
    public async Task<Result<LotMembershipDto>> Handle(GetLotMembershipQuery request, CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty || request.AccountId == Guid.Empty)
        {
            return Result<LotMembershipDto>.Failure(GetLotMembershipErrors.InvalidArguments);
        }

        var assignment = await assignments.FindAsync(request.LotId, request.AccountId, cancellationToken);
        if (assignment is null)
        {
            return Result<LotMembershipDto>.Success(new LotMembershipDto(false, string.Empty));
        }

        return Result<LotMembershipDto>.Success(new LotMembershipDto(true, assignment.Role.ToString()));
    }
}
