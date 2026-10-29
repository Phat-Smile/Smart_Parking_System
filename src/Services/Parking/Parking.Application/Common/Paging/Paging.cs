namespace ParkingSystem.Parking.Application.Common.Paging;

// One rule for every paged list of this service: pages count from 1, a missing or non-positive value falls back to the
// default instead of failing, and the page size is capped so one request cannot ask for a whole table.
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (page is > 0 ? page.Value : 1,
         pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize);

    // The offset is computed as a long: page 2147483647 with 100 rows per page overflows an int.
    public static int Skip(int page, int pageSize) =>
        (int)Math.Clamp((long)(page - 1) * pageSize, 0L, int.MaxValue);
}
