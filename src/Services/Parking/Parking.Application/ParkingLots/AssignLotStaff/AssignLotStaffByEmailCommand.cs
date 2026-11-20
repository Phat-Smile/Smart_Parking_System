using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;

public sealed record AssignLotStaffByEmailCommand(Guid LotId, string Email, Guid OwnerId)
    : IRequest<Result<LotMember>>;

public static class AssignLotStaffErrors
{
    public static readonly Error NotLotOwner =
        new("parking.not_lot_owner", "Chỉ chủ bãi sở hữu bãi đỗ xe này mới có quyền phân công nhân viên.");

    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "Bãi đỗ xe không tồn tại.");

    // Neutral message returned for any invalid, inactive, non-staff or non-existent email to prevent enumeration
    public static readonly Error StaffNotFound =
        new("parking.staff.not_found", "Không tìm thấy tài khoản nhân viên hợp lệ với email đã cung cấp.");
}

public sealed class AssignLotStaffByEmailCommandValidator : AbstractValidator<AssignLotStaffByEmailCommand>
{
    public AssignLotStaffByEmailCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng hợp lệ.");
    }
}

public sealed class AssignLotStaffByEmailCommandHandler(
    ILotOwnershipService lotOwnership,
    IParkingLotAssignmentRepository assignments,
    IStaffAccountLookup staffLookup,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<AssignLotStaffByEmailCommand, Result<LotMember>>
{
    public async Task<Result<LotMember>> Handle(AssignLotStaffByEmailCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify that the caller actually owns the lot
        if (!await lotOwnership.IsLotOwnerAsync(request.LotId, request.OwnerId))
            return Result<LotMember>.Failure(AssignLotStaffErrors.NotLotOwner);

        // 2. Verify that the lot exists
        if (!await assignments.LotExistsAsync(request.LotId, cancellationToken))
            return Result<LotMember>.Failure(AssignLotStaffErrors.LotNotFound);

        // 3. Lookup staff account via Identity gRPC
        var (isFound, accountId) = await staffLookup.LookupStaffByEmailAsync(request.Email, cancellationToken);
        if (!isFound)
            return Result<LotMember>.Failure(AssignLotStaffErrors.StaffNotFound);

        var now = clock.UtcNow;
        var existing = await assignments.FindAsync(request.LotId, accountId, cancellationToken);

        // 4. Idempotent check: if already assigned as Staff, return 200 OK without re-inserting or duplicate audit log
        if (existing is not null)
        {
            if (existing.Role == ParkingLotMemberRole.Staff)
            {
                return Result<LotMember>.Success(new LotMember(
                    existing.LotId,
                    existing.AccountId,
                    existing.Role.ToString(),
                    existing.AssignedAtUtc));
            }

            // Role changed to Staff
            existing.ChangeRole(ParkingLotMemberRole.Staff, request.OwnerId, now);
            auditLog.Record(new AuditEntry(
                AuditActions.LotStaffAssigned,
                request.OwnerId,
                request.LotId,
                nameof(ParkingLotAssignment),
                accountId.ToString(),
                Reason: null,
                now,
                new Dictionary<string, string> { ["role"] = nameof(ParkingLotMemberRole.Staff) }));
            await assignments.SaveChangesAsync(cancellationToken);

            return Result<LotMember>.Success(new LotMember(
                existing.LotId,
                existing.AccountId,
                existing.Role.ToString(),
                existing.AssignedAtUtc));
        }

        // 5. New assignment
        var assignment = ParkingLotAssignment.Create(request.LotId, accountId, ParkingLotMemberRole.Staff, request.OwnerId, now);
        assignments.Add(assignment);

        auditLog.Record(new AuditEntry(
            AuditActions.LotStaffAssigned,
            request.OwnerId,
            request.LotId,
            nameof(ParkingLotAssignment),
            accountId.ToString(),
            Reason: null,
            now,
            new Dictionary<string, string> { ["role"] = nameof(ParkingLotMemberRole.Staff) }));
        await assignments.SaveChangesAsync(cancellationToken);

        return Result<LotMember>.Success(new LotMember(
            assignment.LotId,
            assignment.AccountId,
            assignment.Role.ToString(),
            assignment.AssignedAtUtc));
    }
}
