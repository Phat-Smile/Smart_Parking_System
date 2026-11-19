using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.ExtendExitWindow;
using ParkingSystem.Parking.Application.ParkingSessions.FindOpenParkingSessionByPlate;
using ParkingSystem.Parking.Application.ParkingSessions.FindParkingSessionByQr;
using ParkingSystem.Parking.Application.ParkingSessions.GetParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessionHistory;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.ReissueParkingSessionQr;
using ParkingSystem.Parking.Application.ParkingSessions.RecordExitArrival;
using ParkingSystem.Parking.Application.ParkingSessions.VerifyParkingSessionExit;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingSessionEndpoints
{
    private const string Route = "/api/parking/sessions";

    // Fields stay nullable so a missing field reaches the validator and returns 400 with a clear message.
    // The attributes only describe the contract in Swagger (DOC-002); the FluentValidation validators enforce it.
    public sealed record OpenParkingSessionRequest(
        [property: Required] Guid LotId,
        [property: Required, StringLength(ParkingSession.PlateNumberMaxLength, MinimumLength = 1)] string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))]
        [property: Description("Car or Motorbike, case-insensitive (car and MOTORBIKE are accepted).")]
        string? VehicleType,
        [property: Description("Optional confirmed gate reading from this lot; server uses its recorded arrival time once.")]
        Guid? GateReadingIncidentId = null);

    public sealed record CancelParkingSessionRequest(
        [property: Required, StringLength(ParkingSession.CancelReasonMaxLength, MinimumLength = 1)] string? Reason);

    // US-STAFF-004: the reason of a QR reissue or a manual exit check goes to the audit log (NFR-16).
    public sealed record StaffActionReasonRequest(
        [property: Required, StringLength(ParkingSession.StaffActionReasonMaxLength, MinimumLength = 1)] string? Reason);

    // US-PAY-006: only what the gate reads, plus the staff reason when the plate was checked by hand (US-STAFF-004). Parking takes no money here (lead decision 2026-11-04).
    public sealed record CompleteParkingSessionRequest(
        [property: Required, StringLength(ParkingSession.PlateNumberMaxLength, MinimumLength = 1)] string? PlateNumber,
        [property: Required, StringLength(ParkingSession.QrTokenMaxLength, MinimumLength = 1)] string? QrToken,
        [property: StringLength(ParkingSession.StaffActionReasonMaxLength)] string? ManualVerificationReason = null);

    public static IEndpointRouteBuilder MapParkingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // US-PAY-006. This call confirms the vehicle left; repeating it after Completed returns the same result.
        app.MapPost($"{Route}/{{sessionId:guid}}/complete", async (
                Guid sessionId,
                CompleteParkingSessionRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new CompleteParkingSessionCommand(
                        sessionId,
                        request.PlateNumber ?? string.Empty,
                        request.QrToken ?? string.Empty,
                        actorUserId,
                        request.ManualVerificationReason),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == CompleteParkingSessionErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("CompleteParkingSession")
            .WithSummary("Confirm the vehicle left: checks plate, QR, payment and the 15-minute exit window")
            .Produces<CompletedParkingSession>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // US-PARK-003. Staff or gate only; 401 without a token, 403 without the Staff role or for a lot the caller is not assigned to.
        // Validation failures become 400 in the shared exception handler.
        app.MapPost(Route, async (
                OpenParkingSessionRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Cần biết ai mở phiên để kiểm người đó có được gắn với bãi không (TECH-12).
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new OpenParkingSessionCommand(request.LotId, request.PlateNumber ?? string.Empty, request.VehicleType ?? string.Empty, actorUserId, request.GateReadingIncidentId),
                    cancellationToken);

                // Location points to GET /{sessionId} (lead task S2 2026-10-27; replaces DOC-006).
                if (result.IsSuccess)
                    return Results.Created($"{Route}/{result.Value!.SessionId}", result.Value);

                var statusCode = result.Error == OpenParkingSessionErrors.LotNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : result.Error == OpenParkingSessionErrors.HeldQuotaUnavailable
                            ? StatusCodes.Status503ServiceUnavailable
                            : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("OpenParkingSession")
            .WithSummary("Open a parking session")
            .Produces<OpenedParkingSession>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Staff undo a session when the barrier was opened but the vehicle did not enter (lead decision 2026-10-06).
        app.MapPost($"{Route}/{{sessionId:guid}}/cancel", async (
                Guid sessionId,
                CancelParkingSessionRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // ERD: actor_user_id is identity.users.id, so the token subject must be a uuid.
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new CancelParkingSessionCommand(sessionId, request.Reason ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == CancelParkingSessionErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("CancelParkingSession")
            .WithSummary("Cancel a parking session when the vehicle did not enter")
            .Produces<CancelledParkingSession>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // History includes closed sessions, but never exposes a QR token.
        app.MapGet($"{Route}/me/history", async (
                string? status,
                int? page,
                int? pageSize,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var userId))
                    return Results.Forbid();

                return Results.Ok(await sender.Send(new ListMyParkingSessionHistoryQuery(
                    userId, status, page ?? 1, pageSize ?? 20), cancellationToken));
            })
            .RequireAuthenticatedUser()
            .WithName("ListMyParkingSessionHistory")
            .WithSummary("List my parking sessions, including completed and cancelled sessions, without QR tokens")
            .Produces<MyParkingSessionHistoryPage>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // US-PARK-009. A signed-in customer sees the QR of their own open sessions (BR-05.f).
        app.MapGet($"{Route}/me", async (ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var userId))
                    return Results.Forbid();

                return Results.Ok(await sender.Send(new ListMyParkingSessionsQuery(userId), cancellationToken));
            })
            .RequireAuthenticatedUser()
            .WithName("ListMyParkingSessions")
            .WithSummary("List my open parking sessions with their QR")
            .Produces<IReadOnlyList<MyParkingSession>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // US-PARK-009. Staff or the exit gate scan a session QR; unknown and expired QR codes both return 404 (BR-11.e).
        app.MapGet($"{Route}/by-qr/{{qrToken}}", async (
                string qrToken,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // TECH-12: the caller must be assigned to the lot of the scanned session.
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new FindParkingSessionByQrQuery(qrToken, actorUserId), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ParkingLotAccessErrors.AccessDenied
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status404NotFound;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("FindParkingSessionByQr")
            .WithSummary("Find the open parking session of a scanned QR")
            .Produces<ParkingSessionByQr>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Lead task S2. Any signed-in account may call; the handler allows the session's customer (with the QR) or staff
        // assigned to its lot (without the QR). The guid constraint keeps /me and other named routes on their own.
        app.MapGet($"{Route}/{{sessionId:guid}}", async (
                Guid sessionId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var callerId))
                    return Results.Forbid();

                var result = await sender.Send(new GetParkingSessionQuery(sessionId, callerId), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == GetParkingSessionErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status403Forbidden;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthenticatedUser()
            .WithName("GetParkingSession")
            .WithSummary("Get one parking session (its customer or staff of its lot)")
            .Produces<ParkingSessionDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // US-STAFF-004. At the exit gate the QR is lost or does not match, so staff find the session by plate (BR-11.d).
        app.MapGet($"{Route}/open", async (
                [Required] Guid? lotId,
                [Required] string? plateNumber,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new FindOpenParkingSessionByPlateQuery(lotId ?? Guid.Empty, plateNumber ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ParkingLotAccessErrors.AccessDenied
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status404NotFound;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("FindOpenParkingSessionByPlate")
            .WithSummary("Find the open parking session of a plate in a lot")
            .Produces<OpenParkingSessionByPlate>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // US-STAFF-004. The customer lost the QR: the old QR stops working and a new one is returned (BR-11.d).
        app.MapPost($"{Route}/{{sessionId:guid}}/reissue-qr", async (
                Guid sessionId,
                StaffActionReasonRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new ReissueParkingSessionQrCommand(sessionId, request.Reason ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ReissueParkingSessionQrErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ReissueParkingSessionQr")
            .WithSummary("Reissue the QR of an Active parking session")
            .Produces<ReissuedParkingSessionQr>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // US-STAFF-004. Staff checked the vehicle in person at the exit gate; canExit still requires full payment (BR-11.c).
        app.MapPost($"{Route}/{{sessionId:guid}}/manual-verify", async (
                Guid sessionId,
                StaffActionReasonRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new VerifyParkingSessionExitCommand(sessionId, request.Reason ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == VerifyParkingSessionExitErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("VerifyParkingSessionExit")
            .WithSummary("Record a manual exit check and tell whether the vehicle may leave")
            .Produces<VerifiedParkingSessionExit>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // Q-EXIT-WINDOW (BR-09.d): Staff grant a one-time 15-minute extension to the exit window of a Paid session with mandatory reason.
        app.MapPost($"{Route}/{{sessionId:guid}}/extend-exit-window", async (
                Guid sessionId,
                StaffActionReasonRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new ExtendExitWindowCommand(sessionId, request.Reason ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ExtendExitWindowErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ExtendExitWindow")
            .WithSummary("Extend the 15-minute exit window of a Paid session once with a mandatory reason")
            .Produces<ExtendedExitWindow>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // Q-EXIT-WINDOW (BR-09.d): Gate camera or QR scanner records vehicle arrival at exit gate.
        app.MapPost($"{Route}/{{sessionId:guid}}/exit-arrival", async (
                Guid sessionId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new RecordExitArrivalCommand(sessionId, actorUserId), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == RecordExitArrivalErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("RecordExitArrival")
            .WithSummary("Record the vehicle arrival at the exit gate for a Paid session")
            .Produces<RecordedExitArrival>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
