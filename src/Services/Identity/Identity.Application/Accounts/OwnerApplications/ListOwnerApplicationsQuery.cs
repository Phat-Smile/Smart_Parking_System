using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ListOwnerApplicationsQuery(
    string? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ListOwnerApplicationsResult>>;

public sealed record ListOwnerApplicationsResult(
    IReadOnlyList<OwnerApplicationListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class ListOwnerApplicationsQueryHandler(
    IOwnerApplicationQueries ownerQueries) : IRequestHandler<ListOwnerApplicationsQuery, Result<ListOwnerApplicationsResult>>
{
    public async Task<Result<ListOwnerApplicationsResult>> Handle(ListOwnerApplicationsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        if (pageSize > 100)
            return Result<ListOwnerApplicationsResult>.Failure(OwnerApplicationErrors.InvalidPageSize);

        var (items, totalCount) = await ownerQueries.ListOwnerApplicationsAsync(
            request.Status,
            page,
            pageSize,
            cancellationToken);

        return Result<ListOwnerApplicationsResult>.Success(new ListOwnerApplicationsResult(
            items,
            totalCount,
            page,
            pageSize));
    }
}
