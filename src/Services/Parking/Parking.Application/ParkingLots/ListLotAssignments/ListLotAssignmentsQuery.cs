using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;

namespace ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;

// Admin xem bãi đang có những tài khoản nào được gán (phần xem của PUT / DELETE assignments, US-OWNER-009).
public sealed record ListLotAssignmentsQuery(Guid LotId) : IRequest<Result<IReadOnlyList<LotAssignment>>>;

public sealed record ListOwnerLotAssignmentsQuery(Guid LotId, Guid OwnerId)
    : IRequest<Result<IReadOnlyList<LotAssignment>>>;

public sealed record LotAssignment(
    Guid AccountId,
    string Role,
    DateTimeOffset AssignedAtUtc,
    string? DisplayName = null,
    string? MaskedEmail = null);

public sealed class ListLotAssignmentsQueryHandler(IParkingLotAssignmentRepository assignments)
    : IRequestHandler<ListLotAssignmentsQuery, Result<IReadOnlyList<LotAssignment>>>
{
    public async Task<Result<IReadOnlyList<LotAssignment>>> Handle(ListLotAssignmentsQuery request, CancellationToken cancellationToken)
    {
        // Cùng mã lỗi với PUT / DELETE assignments để một màn hình Admin chỉ xử lý một kiểu.
        if (!await assignments.LotExistsAsync(request.LotId, cancellationToken))
            return Result<IReadOnlyList<LotAssignment>>.Failure(LotMemberErrors.LotNotFound);

        var members = await assignments.ListByLotAsync(request.LotId, cancellationToken);
        return Result<IReadOnlyList<LotAssignment>>.Success(members
            .Select(member => new LotAssignment(member.AccountId, member.Role.ToString(), member.AssignedAtUtc))
            .ToList());
    }
}

public sealed class ListOwnerLotAssignmentsQueryHandler(
    ILotOwnershipService lotOwnership,
    IParkingLotAssignmentRepository assignments,
    IStaffAccountEnricher? staffEnricher = null)
    : IRequestHandler<ListOwnerLotAssignmentsQuery, Result<IReadOnlyList<LotAssignment>>>
{
    public async Task<Result<IReadOnlyList<LotAssignment>>> Handle(ListOwnerLotAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (!await lotOwnership.IsLotOwnerAsync(request.LotId, request.OwnerId))
            return Result<IReadOnlyList<LotAssignment>>.Failure(AssignLotStaffErrors.NotLotOwner);

        if (!await assignments.LotExistsAsync(request.LotId, cancellationToken))
            return Result<IReadOnlyList<LotAssignment>>.Failure(LotMemberErrors.LotNotFound);

        var members = await assignments.ListByLotAsync(request.LotId, cancellationToken);
        if (members.Count == 0)
            return Result<IReadOnlyList<LotAssignment>>.Success([]);

        IReadOnlyDictionary<Guid, (string DisplayName, string MaskedEmail)> enriched =
            new Dictionary<Guid, (string, string)>();

        if (staffEnricher is not null)
        {
            var accountIds = members.Select(m => m.AccountId).Distinct().ToList();
            enriched = await staffEnricher.BatchLookupAccountsAsync(accountIds, cancellationToken);
        }

        return Result<IReadOnlyList<LotAssignment>>.Success(members
            .Select(member =>
            {
                enriched.TryGetValue(member.AccountId, out var details);
                return new LotAssignment(
                    member.AccountId,
                    member.Role.ToString(),
                    member.AssignedAtUtc,
                    details.DisplayName,
                    details.MaskedEmail);
            })
            .ToList());
    }
}
