using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Payments;

public static class ReservationPaymentErrors
{
    public static readonly Error Unavailable =
        new("booking.payment.unavailable", "Payment service is currently unavailable.");

    public static readonly Error DeadlineExceeded =
        new("booking.payment.deadline_exceeded", "The request to Payment service timed out.");

    public static readonly Error CommunicationFailed =
        new("booking.payment.communication_failed", "Failed to communicate with Payment service.");

    public static readonly Error ServiceAuthFailed =
        new("booking.payment.service_auth_failed", "Service authentication with Payment service failed.");
}

