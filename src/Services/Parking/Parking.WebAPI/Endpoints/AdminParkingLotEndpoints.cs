using System.Globalization;
using MediatR;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class AdminParkingLotEndpoints
{
    public static IEndpointRouteBuilder MapAdminParkingLotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/admin/lots", async (
                string? status,
                int? page,
                int? pageSize,
                string? fromDate,
                string? toDate,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                ParkingLotApprovalStatus? approvalStatus = null;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (!ParkingLotApprovalStatusParser.TryParse(status, out var parsedStatus))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["status"] = ["status must be one of: Pending, Approved, Rejected."]
                        });
                    }

                    approvalStatus = parsedStatus;
                }

                var invalidFromDate = ParseVietnamDateFilter(fromDate, "fromDate", isEndDate: false, out var fromInstant);
                if (invalidFromDate is not null)
                    return invalidFromDate;

                var invalidToDate = ParseVietnamDateFilter(toDate, "toDate", isEndDate: true, out var toExclusive);
                if (invalidToDate is not null)
                    return invalidToDate;

                if (fromInstant.HasValue && toExclusive.HasValue && fromInstant >= toExclusive)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["fromDate"] = ["fromDate must be less than or equal to toDate."]
                    });
                }

                var lots = await sender.Send(
                    new GetAdminParkingLotsQuery(approvalStatus, page, pageSize, fromInstant, toExclusive),
                    cancellationToken);
                return Results.Ok(lots);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLots")
            .WithSummary("Get a page of parking lots for administrator review")
            .WithDescription("Optionally filters by status and inclusive Vietnam calendar dates (yyyy-MM-dd). The end date includes the full day.")
            .Produces<PagedResult<AdminParkingLotItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static IResult? ParseVietnamDateFilter(
        string? value,
        string parameterName,
        bool isEndDate,
        out DateTimeOffset? utcBoundary)
    {
        utcBoundary = null;
        if (value is null)
            return null;

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [parameterName] = ["Must be a valid Vietnam calendar date in yyyy-MM-dd format."]
            });
        }

        var boundaryDate = isEndDate ? date.AddDays(1) : date;
        utcBoundary = new DateTimeOffset(
            boundaryDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        return null;
    }
}
