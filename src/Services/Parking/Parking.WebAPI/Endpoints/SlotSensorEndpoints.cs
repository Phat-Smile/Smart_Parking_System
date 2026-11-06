using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

/// <summary>
/// US-MAP-005 (BR-06.d): Endpoint tiếp nhận sự kiện cảm biến vị trí đỗ từ thiết bị IoT / Simulator.
/// Hỗ trợ khử trùng (dedup), xử lý lệch đồng hồ (clock drift) và xử lý sự kiện đến trễ (out-of-order).
/// </summary>
internal static class SlotSensorEndpoints
{
    private const string Route = "/api/parking/slots/sensor-events";

    public sealed record ProcessSlotSensorEventRequest(
        [property: Required]
        [property: Description("Định danh duy nhất của sự kiện từ thiết bị để khử trùng lặp (7 ngày).")]
        Guid? EventId,

        [property: Required, StringLength(64)]
        [property: Description("Mã thiết bị cảm biến gửi sự kiện.")]
        string? DeviceId,

        [property: Required]
        [property: Description("ID bãi đỗ xe.")]
        Guid? LotId,

        [property: Required, StringLength(ParkingSlot.SlotCodeMaxLength)]
        [property: Description("Mã vị trí đỗ (ví dụ: A-01).")]
        string? SlotCode,

        [property: Required]
        [property: Description("Trạng thái cảm biến: 'Empty' hoặc 'Occupied'.")]
        string? State,

        [property: Required]
        [property: Description("Thời điểm phát sinh sự kiện từ thiết bị (event_time).")]
        DateTimeOffset? EventTime);

    public static IEndpointRouteBuilder MapSlotSensorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Route, async (
                ProcessSlotSensorEventRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Enum.TryParse<SensorState>(request.State, ignoreCase: true, out var sensorState))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Yêu cầu không hợp lệ.",
                        detail: "State must be 'Empty' or 'Occupied'.",
                        extensions: ProblemExtensions(httpContext, ProcessSlotSensorEventErrors.InvalidSensorState.Code));
                }

                var command = new ProcessSlotSensorEventCommand(
                    request.EventId ?? Guid.NewGuid(),
                    request.DeviceId ?? string.Empty,
                    request.LotId ?? Guid.Empty,
                    request.SlotCode ?? string.Empty,
                    sensorState,
                    request.EventTime ?? DateTimeOffset.UtcNow);

                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure)
                {
                    var statusCode = result.Error == ProcessSlotSensorEventErrors.SlotNotFound
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status400BadRequest;

                    return Results.Problem(
                        statusCode: statusCode,
                        title: statusCode == StatusCodes.Status404NotFound ? "Không tìm thấy vị trí đỗ." : "Yêu cầu không hợp lệ.",
                        detail: result.Error!.Message,
                        extensions: ProblemExtensions(httpContext, result.Error.Code));
                }

                return Results.Ok(result.Value);
            })
            .RequireDeviceKey()
            .WithName("ProcessSlotSensorEvent")
            .WithSummary("Tiếp nhận sự kiện cảm biến IoT tại từng vị trí đỗ (US-MAP-005, BR-06.d)")
            .WithDescription("Khử trùng lặp theo event_id, lọc sự kiện đến trễ theo event_time, xử lý lệch đồng hồ và cập nhật trạng thái hiệu lực.")
            .Produces<ProcessedSensorEventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Dictionary<string, object?> ProblemExtensions(HttpContext httpContext, string errorCode) =>
        new()
        {
            ["errorCode"] = errorCode,
            ["traceId"] = httpContext.TraceIdentifier
        };
}
