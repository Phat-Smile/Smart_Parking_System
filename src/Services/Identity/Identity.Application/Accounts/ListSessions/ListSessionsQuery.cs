using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Identity.Application.Accounts.ListSessions;

public sealed record ListSessionsQuery(Guid AccountId) : IRequest<Result<ListSessionsResult>>;

public sealed record SessionDto(
    Guid SessionId,
    string? DeviceInfo,
    string? IpAddress,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc);

public sealed record ListSessionsResult(IReadOnlyList<SessionDto> Sessions);

public sealed class ListSessionsQueryHandler(
    IAuthSessionRepository authSessions,
    IClock clock) : IRequestHandler<ListSessionsQuery, Result<ListSessionsResult>>
{
    public async Task<Result<ListSessionsResult>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sessions = await authSessions.ListActiveForAccountAsync(request.AccountId, now, cancellationToken);

        var dtos = sessions.Select(s => new SessionDto(
            s.Id,
            s.DeviceInfo,
            s.IpAddress,
            s.CreatedAtUtc,
            s.LastActivityAtUtc,
            s.AbsoluteExpiresAtUtc)).ToList();

        return Result<ListSessionsResult>.Success(new ListSessionsResult(dtos));
    }
}
