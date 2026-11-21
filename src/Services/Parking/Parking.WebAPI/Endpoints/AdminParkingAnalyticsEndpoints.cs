using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class AdminParkingAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAdminParkingAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/admin/analytics/trends", async (
                string? period,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (period is not (null or "7days" or "30days"))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["period"] = ["period must be either '7days' or '30days'."]
                    });
                }

                var result = await sender.Send(new GetAdminParkingAnalyticsTrendsQuery(period), cancellationToken);
                return Results.Ok(result);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingAnalyticsTrends")
            .WithSummary("Get daily parking submission and approval trends")
            .Produces<AdminParkingAnalyticsTrendsResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
