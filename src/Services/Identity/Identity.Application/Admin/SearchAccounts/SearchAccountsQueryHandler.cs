using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Admin.SearchAccounts;

public sealed class SearchAccountsQueryHandler(IAdminAccountQueries queries)
    : IRequestHandler<SearchAccountsQuery, Result<SearchAccountsResult>>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<SearchAccountsResult>> Handle(SearchAccountsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is > 0 and <= MaxPageSize
            ? request.PageSize
            : DefaultPageSize;

        if (request.PageSize > MaxPageSize)
            return Result<SearchAccountsResult>.Failure(SearchAccountsErrors.InvalidPageSize);

        if (request.Role is not null && !Enum.TryParse<AccountRole>(request.Role, ignoreCase: true, out _))
            return Result<SearchAccountsResult>.Failure(SearchAccountsErrors.InvalidRole);

        if (request.Status is not null && !Enum.TryParse<AccountStatus>(request.Status, ignoreCase: true, out _))
            return Result<SearchAccountsResult>.Failure(SearchAccountsErrors.InvalidStatus);

        var page = request.Page < 1 ? 1 : request.Page;

        var (items, totalCount) = await queries.SearchAsync(
            request.Search,
            request.Role,
            request.Status,
            page,
            pageSize,
            cancellationToken);

        return Result<SearchAccountsResult>.Success(
            new SearchAccountsResult(items, totalCount, page, pageSize));
    }
}
