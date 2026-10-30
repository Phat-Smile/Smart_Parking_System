using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.ListLotParkingSessions;

// Lead task S2 (2026-10-27): staff see the sessions of a lot they are assigned to, e.g. the vehicles still inside
// (status=Active) so a wrong session can be cancelled (Demo A flow 4, step 7).
public sealed record ListLotParkingSessionsQuery(Guid LotId, string? Status, int Page, int PageSize, Guid ActorUserId)
    : IRequest<Result<LotParkingSessionPage>>;

public sealed record LotParkingSessionItem(
    Guid SessionId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EntryTime,
    string? SlotCode,
    string CustomerType);

public sealed record LotParkingSessionPage(IReadOnlyList<LotParkingSessionItem> Items, int TotalCount, int Page, int PageSize);

// Decision 2026-10-27: page 1, 20 per page, at most 100.
public static class LotParkingSessionPaging
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

public sealed class ListLotParkingSessionsQueryValidator : AbstractValidator<ListLotParkingSessionsQuery>
{
    public ListLotParkingSessionsQueryValidator()
    {
        RuleFor(query => query.LotId).NotEmpty();
        RuleFor(query => query.ActorUserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LotParkingSessionPaging.MaxPageSize);
        RuleFor(query => query.Status)
            .Must(value => Enum.GetNames<ParkingSessionStatus>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage($"status must be one of: {string.Join(", ", Enum.GetNames<ParkingSessionStatus>())}.");
    }
}

public sealed class ListLotParkingSessionsQueryHandler(IParkingSessionRepository sessions, IParkingLotAccess lotAccess, ISlotDesignator slots)
    : IRequestHandler<ListLotParkingSessionsQuery, Result<LotParkingSessionPage>>
{
    public async Task<Result<LotParkingSessionPage>> Handle(ListLotParkingSessionsQuery request, CancellationToken cancellationToken)
    {
        // TECH-12: an unknown lot also returns 403, since nobody is assigned to it; the API does not reveal whether it exists.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<LotParkingSessionPage>.Failure(ParkingLotAccessErrors.AccessDenied);

        // No status means every status.
        ParkingSessionStatus? status = string.IsNullOrWhiteSpace(request.Status)
            ? null
            : Enum.Parse<ParkingSessionStatus>(request.Status, ignoreCase: true);

        var (items, totalCount) = await sessions.ListByLotAsync(
            request.LotId, status, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);

        // US-PARK-007: one query for the slots of the whole page.
        var slotCodes = await slots.FindSlotCodesAsync(items.Select(session => session.Id).ToList(), cancellationToken);

        return Result<LotParkingSessionPage>.Success(new LotParkingSessionPage(
            items.Select(session => new LotParkingSessionItem(
                    session.Id,
                    session.PlateNumber,
                    session.VehicleType.ToString(),
                    session.Status.ToString(),
                    session.EntryTime,
                    slotCodes.GetValueOrDefault(session.Id),
                    session.CustomerType.ToString()))
                .ToList(),
            totalCount,
            request.Page,
            request.PageSize));
    }
}
