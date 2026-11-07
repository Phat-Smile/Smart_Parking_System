using FluentValidation;
using MediatR;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessionHistory;

public sealed record ListMyParkingSessionHistoryQuery(Guid UserId, string? Status, int Page, int PageSize)
    : IRequest<MyParkingSessionHistoryPage>;

public sealed record MyParkingSessionHistoryItem(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EntryTime,
    DateTimeOffset? ExitTime,
    DateTimeOffset? CancelledAt);

public sealed record MyParkingSessionHistoryPage(
    IReadOnlyList<MyParkingSessionHistoryItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public interface IMyParkingSessionHistoryReader
{
    Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListAsync(
        Guid userId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken);
}

public sealed class ListMyParkingSessionHistoryQueryValidator : AbstractValidator<ListMyParkingSessionHistoryQuery>
{
    public const int MaxPageSize = 100;

    public ListMyParkingSessionHistoryQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query).Must(query => query.PageSize <= 0 || query.Page <= int.MaxValue / query.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
        RuleFor(query => query.Status)
            .Must(value => Enum.GetNames<ParkingSessionStatus>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.Status));
    }
}

public sealed class ListMyParkingSessionHistoryQueryHandler(IMyParkingSessionHistoryReader reader)
    : IRequestHandler<ListMyParkingSessionHistoryQuery, MyParkingSessionHistoryPage>
{
    public async Task<MyParkingSessionHistoryPage> Handle(
        ListMyParkingSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? (ParkingSessionStatus?)null
            : Enum.Parse<ParkingSessionStatus>(request.Status, ignoreCase: true);
        var (sessions, totalCount) = await reader.ListAsync(
            request.UserId, status, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);

        return new MyParkingSessionHistoryPage(
            sessions.Select(session => new MyParkingSessionHistoryItem(
                session.Id,
                session.LotId,
                session.PlateNumber,
                session.VehicleType.ToString(),
                session.Status.ToString(),
                session.EntryTime,
                session.ExitTime,
                session.CancelledAt)).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
