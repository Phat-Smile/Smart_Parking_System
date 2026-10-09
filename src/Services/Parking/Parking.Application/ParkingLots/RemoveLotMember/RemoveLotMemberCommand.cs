using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.RemoveLotMember;

public sealed record RemoveLotMemberCommand(Guid LotId, Guid AccountId, Guid AdminId) : IRequest<Result<bool>>;

// Gỡ xong có hiệu lực ngay ở request kế tiếp vì quyền theo bãi được tra lại mỗi lần, không nằm trong token.
public sealed class RemoveLotMemberCommandHandler(
    IParkingLotAssignmentRepository assignments,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<RemoveLotMemberCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveLotMemberCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignments.FindAsync(request.LotId, request.AccountId, cancellationToken);
        if (assignment is null)
            return Result<bool>.Failure(LotMemberErrors.MemberNotFound);

        assignments.Remove(assignment);
        auditLog.Record(new AuditEntry(
            AuditActions.LotMemberRemoved,
            request.AdminId,
            request.LotId,
            nameof(ParkingLotAssignment),
            request.AccountId.ToString(),
            Reason: null,
            clock.UtcNow,
            new Dictionary<string, string> { ["role"] = assignment.Role.ToString() }));
        await assignments.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
