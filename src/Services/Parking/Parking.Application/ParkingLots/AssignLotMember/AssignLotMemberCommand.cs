using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;

// Tạm cho Admin gán Chủ bãi / Nhân viên vào bãi, đến khi có luồng chủ bãi tự phân công (US-OWNER-009, S5).
public sealed record AssignLotMemberCommand(Guid LotId, Guid AccountId, string Role, Guid AdminId)
    : IRequest<Result<LotMember>>;

public sealed record LotMember(Guid LotId, Guid AccountId, string Role, DateTimeOffset AssignedAtUtc);

public static class LotMemberErrors
{
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist.");

    public static readonly Error MemberNotFound =
        new("parking.lot.member_not_found", "The account is not assigned to this parking lot.");
}

public sealed class AssignLotMemberCommandValidator : AbstractValidator<AssignLotMemberCommand>
{
    public AssignLotMemberCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.AccountId).NotEmpty();
        RuleFor(command => command.Role)
            .Must(value => Enum.GetNames<ParkingLotMemberRole>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"role must be one of: {string.Join(", ", Enum.GetNames<ParkingLotMemberRole>())}.");
    }
}

// Không kiểm tài khoản có tồn tại ở Identity: khác service, chưa có contract tra tài khoản. Admin chịu trách nhiệm nhập đúng id.
public sealed class AssignLotMemberCommandHandler(
    IParkingLotAssignmentRepository assignments,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<AssignLotMemberCommand, Result<LotMember>>
{
    public async Task<Result<LotMember>> Handle(AssignLotMemberCommand request, CancellationToken cancellationToken)
    {
        if (!await assignments.LotExistsAsync(request.LotId, cancellationToken))
            return Result<LotMember>.Failure(LotMemberErrors.LotNotFound);

        var role = Enum.Parse<ParkingLotMemberRole>(request.Role, ignoreCase: true);
        var now = clock.UtcNow;
        var assignment = await assignments.FindAsync(request.LotId, request.AccountId, cancellationToken);

        bool changed;
        if (assignment is null)
        {
            assignment = ParkingLotAssignment.Create(request.LotId, request.AccountId, role, request.AdminId, now);
            assignments.Add(assignment);
            changed = true;
        }
        else
        {
            changed = assignment.ChangeRole(role, request.AdminId, now);
        }

        // Gọi lại cùng vai trò thì không ghi gì, kể cả audit.
        if (changed)
        {
            auditLog.Record(new AuditEntry(
                AuditActions.LotMemberAssigned,
                request.AdminId,
                request.LotId,
                nameof(ParkingLotAssignment),
                request.AccountId.ToString(),
                Reason: null,
                now,
                new Dictionary<string, string> { ["role"] = role.ToString() }));
            await assignments.SaveChangesAsync(cancellationToken);
        }

        return Result<LotMember>.Success(new LotMember(assignment.LotId, assignment.AccountId, assignment.Role.ToString(), assignment.AssignedAtUtc));
    }
}
