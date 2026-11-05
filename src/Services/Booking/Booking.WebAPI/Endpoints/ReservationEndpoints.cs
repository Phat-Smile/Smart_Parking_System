using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Reservations.Queries.GetMyReservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Identity;

namespace ParkingSystem.Booking.WebAPI.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/booking")
            .RequireAuthorization();

        // 1. Tạo yêu cầu giữ chỗ tạm thời (US-RES-005)
        group.MapPost("/holds", async (
            HttpContext httpContext,
            ICurrentUser currentUser,
            CreateReservationHoldRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var vehicleGroup = VehicleGroup.Car;
            if (!string.IsNullOrWhiteSpace(request.VehicleGroup) &&
                !Enum.TryParse<VehicleGroup>(request.VehicleGroup, ignoreCase: true, out vehicleGroup))
            {
                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, ReservationCapacityErrors.VehicleGroupNotReservable);
            }

            var resType = ReservationType.Scheduled;
            if (!string.IsNullOrWhiteSpace(request.ReservationType) &&
                !Enum.TryParse<ReservationType>(request.ReservationType, ignoreCase: true, out resType))
            {
                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, ReservationCapacityErrors.InvalidTimeWindow);
            }

            var reservationId = Guid.NewGuid();
            var command = new CreateTemporaryHoldCommand(
                reservationId,
                userId,
                request.VehicleId ?? Guid.Empty,
                request.PlateNumber,
                request.LotId,
                vehicleGroup,
                resType,
                request.AppointmentTime,
                request.DurationMinutes,
                request.PrepaidAmount,
                request.PriceSnapshot);

            var result = await sender.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                return ReservationEndpointErrorMapper.ToProblem(httpContext, result.Error!);
            }

            var r = result.Value!;
            var response = new ReservationHoldResponse(
                r.Id,
                r.LotId,
                r.VehicleId,
                r.PlateNumber,
                r.VehicleGroup.ToString(),
                r.ReservationType.ToString(),
                r.Status.ToString(),
                r.AppointmentTime,
                r.DurationMinutes,
                r.TempHoldExpiresAt,
                r.WindowStartAt,
                r.WindowEndAt,
                r.PrepaidAmount,
                r.PriceSnapshot,
                r.CreatedAt);

            return Results.Created($"/api/booking/reservations/{r.Id}", response);
        })
        .WithName("CreateReservationHold")
        .WithSummary("Tạo yêu cầu giữ chỗ tạm thời 10 phút (US-RES-005). VehicleId và VehicleGroup từ client bị bỏ qua và được xác minh từ Identity.")
        .Produces<ReservationHoldResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // 2. Xác nhận đặt chỗ & trừ tiền cọc từ ví
        group.MapPost("/reservations/{id:guid}/confirm", async (
            Guid id,
            HttpContext httpContext,
            ICurrentUser currentUser,
            ConfirmReservationApiRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new ConfirmReservationCommand(id, userId, request?.PaymentMethod, request?.DepositAmount ?? 0);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return ReservationEndpointErrorMapper.ToProblem(httpContext, result.Error!);
            }

            return Results.Ok(result.Value);
        })
        .WithName("ConfirmReservation")
        .WithSummary("Xác nhận đặt chỗ sau khi thanh toán cọc. DepositAmount bị bỏ qua, sẽ xóa khi có Payment.")
        .Produces<ConfirmedReservationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 3. Tra cứu danh sách đặt chỗ của tôi
        group.MapGet("/reservations/me", async (
            HttpContext httpContext,
            ICurrentUser currentUser,
            string? status,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var query = new GetMyReservationsQuery(userId, status);
            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error!);
        })
        .WithName("GetMyReservations")
        .WithSummary("Tra cứu danh sách đặt chỗ của người dùng hiện tại")
        .Produces<IReadOnlyList<ReservationSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // 4. Hủy đặt chỗ
        group.MapPost("/reservations/{id:guid}/cancel", async (
            Guid id,
            HttpContext httpContext,
            ICurrentUser currentUser,
            CancelReservationApiRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new CancelReservationCommand(id, userId, request?.Reason);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                var statusCode = result.Error == ReservationErrors.NotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ReservationErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest;

                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            }

            return Results.Ok(result.Value);
        })
        .WithName("CancelReservation")
        .WithSummary("Hủy đặt chỗ và hoàn cọc")
        .Produces<CancelledReservationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 5. Check-in đặt chỗ khi xe vào cổng (US-PARK-004 / MF-08)
        group.MapPost("/reservations/check-in", async (
            HttpContext httpContext,
            CheckInReservationApiRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CheckInReservationCommand(
                request.LotId,
                request.PlateNumber,
                request.EntryTime ?? DateTimeOffset.UtcNow,
                request.SessionId);

            var result = await sender.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error!);
            }

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("CheckInReservation")
        .WithSummary("Check-in đặt chỗ tại cổng khi xe đến bãi (MF-08)")
        .Produces<CheckInReservationResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}

public sealed record CheckInReservationApiRequest(
    [Required] Guid LotId,
    [Required] string PlateNumber,
    DateTimeOffset? EntryTime,
    Guid? SessionId);

public sealed record CreateReservationHoldRequest(
    [Required] Guid LotId,
    Guid? VehicleId,
    [Required] string PlateNumber,
    string? VehicleGroup,
    string? ReservationType,
    DateTimeOffset AppointmentTime,
    int? DurationMinutes,
    decimal PrepaidAmount,
    string? PriceSnapshot = null);

public sealed record ConfirmReservationApiRequest(
    string? PaymentMethod,
    decimal DepositAmount);

public sealed record CancelReservationApiRequest(
    string? Reason);

public sealed record ReservationHoldResponse(
    Guid ReservationId,
    Guid LotId,
    Guid VehicleId,
    string PlateNumber,
    string VehicleGroup,
    string ReservationType,
    string Status,
    DateTimeOffset AppointmentTime,
    int? DurationMinutes,
    DateTimeOffset? TempHoldExpiresAt,
    DateTimeOffset WindowStartAt,
    DateTimeOffset WindowEndAt,
    decimal PrepaidAmount,
    string? PriceSnapshot,
    DateTimeOffset CreatedAt);
