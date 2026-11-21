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

                var invalidFromDate = ParseUtcDateFilter(fromDate, "fromDate", out var fromInstant);
                if (invalidFromDate is not null)
                    return invalidFromDate;

                var invalidToDate = ParseUtcDateFilter(toDate, "toDate", out var toInstant);
                if (invalidToDate is not null)
                    return invalidToDate;

                if (fromInstant.HasValue && toInstant.HasValue && fromInstant > toInstant)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["fromDate"] = ["fromDate must be less than or equal to toDate."]
                    });
                }

                var lots = await sender.Send(
                    new GetAdminParkingLotsQuery(approvalStatus, page, pageSize, fromInstant, toInstant),
                    cancellationToken);
                return Results.Ok(lots);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLots")
            .WithSummary("Get a page of parking lots for administrator review")
            .WithDescription("Optionally filters by status and inclusive UTC submission instants (fromDate and toDate).")
            .Produces<PagedResult<AdminParkingLotItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static IResult? ParseUtcDateFilter(string? value, string parameterName, out DateTimeOffset? instant)
    {
        instant = null;
        if (value is null)
            return null;

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [parameterName] = ["Must be a valid ISO 8601 date/time."]
            });
        }

        instant = parsed;
        return null;
    }
}
