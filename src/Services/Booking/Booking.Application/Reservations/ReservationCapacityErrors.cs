using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// Domain and application error definitions for booking capacity guard and temporary hold operations.
/// </summary>
public static class ReservationCapacityErrors
{
    public static readonly Error CapacityExceeded =
        new("booking.capacity.exceeded", "The reservation ceiling for this parking lot has been reached for the requested time window.");

    public static readonly Error NoAvailableSlots =
        new("booking.capacity.no_available_slots", "No available slots right now for the requested parking lot.");

    public static readonly Error CapacityInfoUnavailable =
        new("booking.capacity.info_unavailable", "Unable to retrieve parking lot capacity information.");

    public static readonly Error VehicleGroupNotReservable =
        new("booking.capacity.vehicle_group_not_reservable", "Only cars are eligible for reservation.");

    public static readonly Error InvalidTimeWindow =
        new("booking.capacity.invalid_time_window", "The requested appointment time or duration is invalid.");

    public static readonly Error LockTimeout =
        new("booking.capacity.lock_timeout", "Could not acquire capacity lock within the allotted time. Please retry.");

    public static readonly Error UserActiveReservationsLimitExceeded =
        new("booking.reservation.user_active_limit_exceeded", "The user has reached the maximum limit of active reservations.");

    public static readonly Error PlateReservationOverlap =
        new("booking.reservation.plate_overlap", "An active reservation already exists for this vehicle plate during the requested time window.");
}

