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
                // A sensor must provide stable identifiers and its original event time.
                // Generating either at the API would silently disable deduplication and ordering.
                if ((request.EventId is null || request.EventId == Guid.Empty)
                    || (request.LotId is null || request.LotId == Guid.Empty)
                    || string.IsNullOrWhiteSpace(request.DeviceId)
                    || request.DeviceId.Length > 64
                    || string.IsNullOrWhiteSpace(request.SlotCode)
                    || request.SlotCode.Length > ParkingSlot.SlotCodeMaxLength
                    || (request.EventTime is null || request.EventTime == default(DateTimeOffset)))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Yêu cầu không hợp lệ.",
                        detail: "EventId, DeviceId, LotId, SlotCode and EventTime are required and must be valid.",
                        extensions: ProblemExtensions(httpContext, ProcessSlotSensorEventErrors.InvalidSensorPayload.Code));
                }

                // Enum.TryParse accepts numeric input such as "1"; the device wire contract does not.
                var stateText = request.State?.Trim();
                if (!string.Equals(stateText, nameof(SensorState.Empty), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(stateText, nameof(SensorState.Occupied), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Yêu cầu không hợp lệ.",
                        detail: "State must be 'Empty' or 'Occupied'.",
                        extensions: ProblemExtensions(httpContext, ProcessSlotSensorEventErrors.InvalidSensorState.Code));
                }

                var sensorState = Enum.Parse<SensorState>(stateText!, ignoreCase: true);
                var command = new ProcessSlotSensorEventCommand(
                    request.EventId!.Value,
                    request.DeviceId!.Trim(),
                    request.LotId!.Value,
                    request.SlotCode!.Trim(),
                    sensorState,
                    request.EventTime!.Value);

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
