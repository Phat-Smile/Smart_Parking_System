using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.RemoveLotStaff;

public sealed record RemoveLotStaffByOwnerCommand(Guid LotId, Guid AccountId, Guid OwnerId)
    : IRequest<Result<bool>>;

public sealed class RemoveLotStaffByOwnerCommandHandler(
    ILotOwnershipService lotOwnership,
    IParkingLotAssignmentRepository assignments,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<RemoveLotStaffByOwnerCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveLotStaffByOwnerCommand request, CancellationToken cancellationToken)
    {
        if (!await lotOwnership.IsLotOwnerAsync(request.LotId, request.OwnerId))
            return Result<bool>.Failure(AssignLotStaffErrors.NotLotOwner);

        var assignment = await assignments.FindAsync(request.LotId, request.AccountId, cancellationToken);
        if (assignment is null)
            return Result<bool>.Failure(LotMemberErrors.MemberNotFound);

        if (assignment.Role != ParkingLotMemberRole.Staff)
            return Result<bool>.Failure(LotMemberErrors.MemberNotFound);

        assignments.Remove(assignment);
        auditLog.Record(new AuditEntry(
            AuditActions.LotStaffRemoved,
            request.OwnerId,
            request.LotId,
            nameof(ParkingLotAssignment),
            request.AccountId.ToString(),
            Reason: null,
            clock.UtcNow,
            new Dictionary<string, string> { ["role"] = nameof(ParkingLotMemberRole.Staff) }));
        await assignments.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
