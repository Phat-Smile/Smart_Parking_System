using MediatR;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class AdminParkingLotReviewEndpoints
{
    public static IEndpointRouteBuilder MapAdminParkingLotReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/admin/lots/{parkingLotId:guid}", async (
                Guid parkingLotId, ISender sender, CancellationToken cancellationToken) =>
            {
                var detail = await sender.Send(new GetAdminParkingLotDetailQuery(parkingLotId), cancellationToken);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLotDetail")
            .WithSummary("Read parking-lot detail for administrator review, including Pending and Rejected")
            .Produces<AdminParkingLotDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/parking/admin/lots/{parkingLotId:guid}/layout", async (
                Guid parkingLotId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAdminParkingLayoutQuery(parkingLotId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLotLayout")
            .WithSummary("Read any parking lot layout for administrator review; never updates layout")
            .Produces<ParkingLotLayout>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/parking/admin/lots/{parkingLotId:guid}/pricing/{vehicleType}", async (
                Guid parkingLotId, string vehicleType, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAdminPricingPolicyQuery(parkingLotId, vehicleType), cancellationToken);
                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                // An invalid vehicle type is a client error; missing lot or price is 404.
                var code = result.Error!.Code;
                var status = code.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
                return Results.Problem(statusCode: status, title: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = code });
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLotPricing")
            .WithSummary("Read current pricing policy for any lot approval status")
            .Produces<PricingPolicyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
