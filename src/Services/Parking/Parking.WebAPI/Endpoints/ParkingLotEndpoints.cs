using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.ReviewParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.GetParkingLotDetail;
using ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.AspNetCore;
using ParkingSystem.Parking.Application.ParkingLayout.GetMotorbikeAvailability;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Application.ParkingLots.SearchNearbyParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetLotPricing;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.WebAPI.Routing;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotEndpoints
{
    public sealed record RegisterParkingLotRequest(
        string? Name,
        string? Address,
        decimal? Latitude,
        decimal? Longitude,
        string? Type,
        ParkingLotFeatures? Features = null);

    private static IReadOnlyList<string> ParseFilterValues(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant()).Distinct().ToArray();

    // Only a status name selects a status: Enum.TryParse also takes numbers and comma lists (see ParkingLotApprovalStatusParser).
    private static IResult? ParseStatusFilter(string? status, out ParkingLotApprovalStatus? approvalStatus)
    {
        approvalStatus = null;
        if (string.IsNullOrWhiteSpace(status))
            return null;

        if (!ParkingLotApprovalStatusParser.TryParse(status, out var parsed))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["status must be one of: Pending, Approved, Rejected."]
            });
        }

        approvalStatus = parsed;
        return null;
    }

    public static IEndpointRouteBuilder MapParkingLotEndpoints(this IEndpointRouteBuilder app)
    {
        // US-OWNER-001: role is checked against the Identity JWT; owner_user_id comes from "sub", never from the request.
        app.MapPost("/api/parking/lots", async (
                RegisterParkingLotRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerUserId) || ownerUserId == Guid.Empty)
                    return Results.Forbid();

                RegisteredParkingLot registered;
                try
                {
                    registered = await sender.Send(
                        new RegisterParkingLotCommand(
                            ownerUserId,
                            request.Name ?? string.Empty,
                            request.Address ?? string.Empty,
                            request.Latitude,
                            request.Longitude,
                            request.Type ?? string.Empty,
                            request.Features),
                        cancellationToken);
                }
                catch (DuplicateParkingLotException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Parking lot already registered.",
                        detail: "This owner already has a parking lot with this name.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "parking.lot_duplicate"
                        });
                }

                // Pending lots are intentionally hidden by GET /api/parking/lots/{id}.
                // Do not return a Location URL that would respond 404 until admin approval.
                return Results.Created((string?)null, registered);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("RegisterParkingLot")
            .WithSummary("Register a parking lot for administrator review")
            .WithDescription("ParkingLotOwner only. New parking lots start Pending and are not visible in public catalog/detail.")
            .Produces<RegisteredParkingLot>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // D2-BE-1 / API-PK-01: a page of the lots the caller owns, whatever their approval status (docs/api/Parking-Owner-Admin-Lots.md).
        app.MapGet("/api/parking/owner/lots", async (
                string? status,
                int? page,
                int? pageSize,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerUserId) || ownerUserId == Guid.Empty)
                    return Results.Forbid();

                var invalidStatus = ParseStatusFilter(status, out var approvalStatus);
                if (invalidStatus is not null)
                    return invalidStatus;

                var lots = await sender.Send(
                    new GetOwnerParkingLotsQuery(ownerUserId, approvalStatus, page, pageSize),
                    cancellationToken);
                return Results.Ok(lots);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("GetOwnerParkingLots")
            .WithSummary("Get a page of the parking lots owned by the authenticated owner")
            .Produces<PagedResult<OwnerParkingLotItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // API-PK-02: an owner reads one of their lots in any approval state (Pending and Rejected included).
        app.MapGet("/api/parking/owner/lots/{lotId:guid}", async (
                Guid lotId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerUserId) || ownerUserId == Guid.Empty)
                    return Results.Forbid();

                var result = await sender.Send(new GetOwnerParkingLotDetailQuery(lotId, ownerUserId), cancellationToken);
                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ParkingLotAccessErrors.AccessDenied
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status404NotFound;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("GetOwnerParkingLotDetail")
            .WithSummary("Get one parking lot of the authenticated owner, whatever its approval status")
            .Produces<OwnerParkingLotDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/parking/lots", async (
                DateTimeOffset? arrivalAt,
                string? vehicleType,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                VehicleType? selectedVehicleType = null;
                if (vehicleType is not null)
                {
                    // Enum.TryParse accepts integer strings (e.g. "1"); allow enum names only.
                    if (!Enum.GetNames<VehicleType>().Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["vehicleType"] = ["vehicleType must be one of: Car, Motorbike."]
                        });
                    }

                    selectedVehicleType = Enum.Parse<VehicleType>(vehicleType, ignoreCase: true);
                }

                var parkingLots = arrivalAt.HasValue
                    ? await sender.Send(new ListParkingLotsAtArrivalQuery(arrivalAt.Value, selectedVehicleType), cancellationToken)
                    : await sender.Send(new ListParkingLotsQuery(selectedVehicleType), cancellationToken);
                return Results.Ok(parkingLots);
            })
            .AllowAnonymous()
            .WithName("ListParkingLots")
            .WithSummary("List approved parking lots, optionally filtered by vehicle type and arrival time")
            .WithDescription("Returns approved lots only. Optional vehicleType=Car|Motorbike matches a configured zone. Optional arrivalAt (ISO 8601 with offset) keeps only lots open at that time, using configured local opening hours. Without filters the original catalog is returned. Authentication is not required.")
            .Produces<IReadOnlyList<ParkingLotCatalogItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        app.MapGet("/api/parking/lots/search", async (
                decimal? latitude, decimal? longitude, double? radiusKm, string? vehicleType,
                decimal? maxPrice, bool? availableOnly, bool? prebookableOnly,
                string? compatibility, string? amenities, string? sort, int? limit,
                ISender sender, [FromServices] OsrmMatrixClient routing, CancellationToken cancellationToken) =>
            {
                VehicleType? selectedType = null;
                if (vehicleType is not null)
                {
                    if (!Enum.GetNames<VehicleType>().Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
                        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleType"] = ["Use Car or Motorbike."] });
                    selectedType = Enum.Parse<VehicleType>(vehicleType, true);
                }
                var results = await sender.Send(new SearchCustomerLotsQuery(latitude, longitude, radiusKm ?? 5,
                    selectedType, maxPrice, availableOnly ?? false, sort ?? "recommended", limit ?? 50,
                    prebookableOnly ?? false, ParseFilterValues(compatibility), ParseFilterValues(amenities)), cancellationToken);
                // A single OSRM Table call covers the first ten candidates, per NFR-04.
                // Other candidates keep a null ETA; no Haversine-derived driving time.
                if (latitude.HasValue && longitude.HasValue && results.Count > 0)
                {
                    var routed = results.Take(10).ToArray();
                    var minutes = await routing.GetMinutesAsync(latitude.Value, longitude.Value, routed,
                        selectedType?.ToString() ?? "Any", cancellationToken);
                    results = results.Select(item => item with {
                        DrivingTimeMinutes = minutes.TryGetValue(item.Id, out var eta) ? eta : null
                    }).ToArray();
                }
                return Results.Ok(results);
            })
            .AllowAnonymous()
            .WithName("SearchCustomerParkingLots")
            .WithSummary("Search approved lots by price, live availability, vehicle type and distance")
            .Produces<IReadOnlyList<CustomerLotSearchItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        app.MapGet("/api/parking/lots/nearby", async (
                decimal latitude,
                decimal longitude,
                double? radiusKm,
                int? limit,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var results = await sender.Send(
                    new SearchNearbyParkingLotsQuery(latitude, longitude, radiusKm ?? 5, limit ?? 20),
                    cancellationToken);
                return Results.Ok(results);
            })
            .AllowAnonymous()
            .WithName("SearchNearbyParkingLots")
            .WithSummary("Find approved parking lots near coordinates")
            .WithDescription("Search by latitude/longitude. radiusKm defaults to 5 (max 50); limit defaults to 20 (max 100). Sorted by distance.")
            .Produces<IReadOnlyList<NearbyParkingLotItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/parking/lots/{parkingLotId}", async (
                Guid parkingLotId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var parkingLot = await sender.Send(
                    new GetParkingLotDetailQuery(parkingLotId),
                    cancellationToken);
                if (parkingLot is null)
                    return Results.NotFound();

                // MOD-PK-01: prices and free spaces come with the detail, so the detail page needs one request.
                var pricing = await sender.Send(new GetLotPricingQuery(parkingLotId), cancellationToken);
                var availability = await sender.Send(new GetLotAvailabilityQuery(parkingLotId), cancellationToken);

                return Results.Ok(new PublicParkingLotDetailResponse(
                    parkingLot.Id,
                    parkingLot.Name,
                    parkingLot.Address,
                    parkingLot.Latitude,
                    parkingLot.Longitude,
                    parkingLot.Type,
                    parkingLot.OpeningHours,
                    pricing.IsSuccess ? pricing.Value!.Items : [],
                    availability.IsSuccess ? availability.Value : null,
                    parkingLot.Features?.Compatibility ?? [],
                    parkingLot.Features?.Amenities ?? [],
                    parkingLot.Features?.Prebookable == true && parkingLot.Features.SupportsCar,
                    parkingLot.Features?.Prebookable == true && parkingLot.Features.SupportsCar ? ["car"] : [],
                    parkingLot.FloorCount,
                    parkingLot.CarCapacity,
                    parkingLot.MotorbikeCapacity,
                    parkingLot.PublicInfo));
            })
            .AllowAnonymous()
            .WithName("GetParkingLotDetail")
            .WithSummary("Get an approved parking lot")
            .WithDescription("Returns the public detail of an approved parking lot with its current prices and free spaces. Pending and rejected parking lots are not exposed.")
            .Produces<PublicParkingLotDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // D2-BE-2: Admin gets parking lots, optionally filtered by status (Pending, Approved, Rejected)
        app.MapGet("/api/parking/admin/lots", async (
                string? status,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var invalidStatus = ParseStatusFilter(status, out var approvalStatus);
                if (invalidStatus is not null)
                    return invalidStatus;

                var lots = await sender.Send(new GetAdminParkingLotsQuery(approvalStatus, page, pageSize), cancellationToken);
                return Results.Ok(lots);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("GetAdminParkingLots")
            .WithSummary("Get a page of parking lots for administrator review")
            .Produces<PagedResult<AdminParkingLotItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // US-ADMIN-002: authorized administrators can decide only Pending lots.
        app.MapPost("/api/parking/admin/lots/{parkingLotId:guid}/approve", async (
                Guid parkingLotId,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorId))
                    return Results.Forbid();

                var result = await sender.Send(new ReviewParkingLotCommand(
                    parkingLotId, ParkingLotApprovalStatus.Approved, null, actorId), cancellationToken);
                return ToReviewResult(result);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("ApproveParkingLot")
            .WithSummary("Approve a pending parking lot")
            .Produces<ReviewedParkingLot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/parking/admin/lots/{parkingLotId:guid}/reject", async (
                Guid parkingLotId,
                RejectParkingLotRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorId))
                    return Results.Forbid();

                var result = await sender.Send(new ReviewParkingLotCommand(
                    parkingLotId, ParkingLotApprovalStatus.Rejected, request.Reason, actorId), cancellationToken);
                return ToReviewResult(result);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("RejectParkingLot")
            .WithSummary("Reject a pending parking lot with a reason")
            .Produces<ReviewedParkingLot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // API-PK-06: free car slots and motorbike spaces of an approved lot, for the customer detail page.
        app.MapGet("/api/parking/lots/{parkingLotId:guid}/availability", async (
                Guid parkingLotId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetLotAvailabilityQuery(parkingLotId), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .AllowAnonymous()
            .WithName("GetParkingLotAvailability")
            .WithSummary("Get the free car slots and motorbike spaces of an approved parking lot")
            .Produces<LotAvailability>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // US-PARK-008. The staff screen reads how many motorbike spaces are left before accepting a motorbike.
        app.MapGet("/api/parking/lots/{parkingLotId:guid}/motorbike-availability", async (
                Guid parkingLotId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // TECH-12: the caller must be assigned to this lot.
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new GetMotorbikeAvailabilityQuery(parkingLotId, actorUserId), cancellationToken);
                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ParkingLotAccessErrors.AccessDenied
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status404NotFound;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("GetMotorbikeAvailability")
            .WithSummary("Get the motorbike spaces left in a lot")
            .Produces<MotorbikeAvailabilityResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public sealed record RejectParkingLotRequest(string? Reason);

    // MOD-PK-01: the fields of ParkingLotDetail, unchanged, plus prices and free spaces.
    public sealed record PublicParkingLotDetailResponse(
        Guid Id,
        string Name,
        string Address,
        decimal Latitude,
        decimal Longitude,
        string Type,
        WeeklyOpeningHours? OpeningHours,
        IReadOnlyList<PricingPolicyDto> Pricing,
        LotAvailability? Availability,
        IReadOnlyList<string>? Compatibility = null,
        IReadOnlyList<string>? Amenities = null,
        bool Prebookable = false,
        IReadOnlyList<string>? BookableVehicleTypes = null,
        int? FloorCount = null,
        int? CarCapacity = null,
        int? MotorbikeCapacity = null,
        ParkingLotPublicInfo? PublicInfo = null);

    private static IResult ToReviewResult(ParkingSystem.BuildingBlocks.Results.Result<ReviewedParkingLot> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        var statusCode = result.Error == ReviewParkingLotErrors.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;
        return Results.Problem(
            statusCode: statusCode,
            title: result.Error!.Message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
    }
}
