using Microsoft.AspNetCore.Http;
using ParkingSystem.AspNetCore;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.WebAPI.Endpoints;

public sealed record EndpointErrorMapping(int StatusCode, Error PublicError);

/// <summary>
/// Centralized error mapper bridging application domain and ownership errors
/// to HTTP ProblemDetails representations with strict information-hiding policies.
/// </summary>
public static class ReservationEndpointErrorMapper
{
    private static readonly Error CommonOwnershipMismatch =
        new(VehicleOwnershipErrors.VehicleOwnershipMismatch.Code, "Xe không thuộc tài khoản của bạn.");

    private static readonly Error CommonVehicleVerificationUnavailable =
        new("booking.vehicle.service_unavailable", "Dịch vụ xác minh xe tạm thời không khả dụng, vui lòng thử lại.");

    /// <summary>
    /// Maps internal <see cref="VehicleOwnershipErrors"/> to sanitized public responses per Table 5.2.
    /// </summary>
    public static EndpointErrorMapping MapOwnershipError(Error internalError)
    {
        if (internalError == VehicleOwnershipErrors.EmptyPlate ||
            internalError == VehicleOwnershipErrors.InvalidPlateFormat)
        {
            return new EndpointErrorMapping(
                StatusCodes.Status400BadRequest,
                new Error(internalError.Code, "Biển số xe không hợp lệ."));
        }

        // Information-hiding: "Not Registered" and "Ownership Mismatch" share identical 403 status, code, and message.
        if (internalError == VehicleOwnershipErrors.VehicleNotRegistered ||
            internalError == VehicleOwnershipErrors.VehicleOwnershipMismatch)
        {
            return new EndpointErrorMapping(
                StatusCodes.Status403Forbidden,
                CommonOwnershipMismatch);
        }

        if (internalError == VehicleOwnershipErrors.UnsupportedVehicleType)
        {
            return new EndpointErrorMapping(
                StatusCodes.Status400BadRequest,
                new Error(VehicleOwnershipErrors.UnsupportedVehicleType.Code, "Chỉ hỗ trợ đặt chỗ cho ô tô."));
        }

        if (internalError == VehicleOwnershipErrors.AccountNotEligible)
        {
            return new EndpointErrorMapping(
                StatusCodes.Status403Forbidden,
                new Error(VehicleOwnershipErrors.AccountNotEligible.Code, "Tài khoản của bạn chưa đủ điều kiện đặt chỗ."));
        }

        if (internalError == VehicleOwnershipErrors.ServiceUnavailable ||
            internalError == VehicleOwnershipErrors.DeadlineExceeded ||
            internalError == VehicleOwnershipErrors.CommunicationFailed ||
            internalError == VehicleOwnershipErrors.ServiceAuthFailed ||
            internalError == VehicleOwnershipErrors.RequestCancelled)
        {
            return new EndpointErrorMapping(
                StatusCodes.Status503ServiceUnavailable,
                CommonVehicleVerificationUnavailable);
        }

        return new EndpointErrorMapping(
            StatusCodes.Status500InternalServerError,
            new Error("booking.vehicle.unknown_error", "Lỗi xác thực phương tiện không xác định."));
    }

    /// <summary>
    /// Creates an <see cref="IResult"/> ProblemDetails for any application error across Reservation endpoints.
    /// </summary>
    public static IResult ToProblem(HttpContext context, Error error)
    {
        if (error.Code.StartsWith("booking.vehicle.", StringComparison.OrdinalIgnoreCase))
        {
            var mapping = MapOwnershipError(error);
            return BusinessProblem.Create(context, mapping.StatusCode, mapping.PublicError);
        }

        if (error.Code == ReservationErrors.NotFound.Code)
        {
            return BusinessProblem.Create(context, StatusCodes.Status404NotFound, error);
        }

        if (error.Code == ReservationErrors.AccessDenied.Code)
        {
            return BusinessProblem.Create(context, StatusCodes.Status403Forbidden, error);
        }

        if (error.Code == ReservationErrors.CannotCancel.Code ||
            error.Code == ReservationErrors.InsufficientWalletBalance.Code ||
            error.Code == ReservationErrors.PaymentFailed.Code ||
            error.Code == ReservationCapacityErrors.CapacityExceeded.Code ||
            error.Code == ReservationCapacityErrors.NoAvailableSlots.Code ||
            error.Code == ReservationCapacityErrors.LotReservationsDisabled.Code)
        {
            return BusinessProblem.Create(context, StatusCodes.Status409Conflict, error);
        }

        if (error.Code == ReservationCapacityErrors.CapacityInfoUnavailable.Code ||
            error.Code == ReservationCapacityErrors.LockTimeout.Code ||
            error.Code == ReservationErrors.PaymentNotAvailable.Code ||
            error.Code == ReservationErrors.PaymentUnconfirmed.Code)
        {
            return BusinessProblem.Create(context, StatusCodes.Status503ServiceUnavailable, error);
        }

        return BusinessProblem.Create(context, StatusCodes.Status400BadRequest, error);
    }
}

