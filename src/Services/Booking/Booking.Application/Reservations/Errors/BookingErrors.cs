using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Errors;

public static class BookingErrors
{
    public static readonly Error AccountActiveLimitExceeded = new(
        "booking.abuse.account_limit_exceeded",
        "The account has reached the maximum permitted number of active reservations (BR-01.f).");

    public static readonly Error PlateOverlappingReservationExists = new(
        "booking.abuse.plate_overlap",
        "This license plate already has an active reservation in an overlapping time window (BR-01.f).");

    public static readonly Error ReservationCeilingExceeded = new(
        "booking.capacity.ceiling_exceeded",
        "No reservation capacity is available for this parking lot in the requested time window (BR-02.c).");

    public static readonly Error PhysicalSlotsUnavailable = new(
        "booking.capacity.physical_slots_unavailable",
        "No available physical parking slots for immediate reservation hold (BR-02.c).");

    public static readonly Error VehicleGroupNotSupported = new(
        "booking.invalid_vehicle_group",
        "Only passenger cars (Car) can reserve parking capacity (BR-01.a).");

    public static readonly Error BookingDisabled = new(
        "booking.lot_booking_disabled",
        "Online booking is currently disabled for this parking lot.");

    public static readonly Error LotCapacityUnavailable = new(
        "booking.lot_capacity_unavailable",
        "Unable to determine valid capacity or availability for this parking lot.");

    public static readonly Error ReservationAlreadyExistsConflict = new(
        "booking.idempotency_conflict",
        "A reservation with this ID already exists with different parameters.");

    public static readonly Error ScheduledDurationExceeded = new(
        "booking.scheduled.duration_exceeded",
        "Scheduled reservation duration cannot exceed 24 hours (BR-01.a).");

    public static readonly Error ScheduledLeadTimeExceeded = new(
        "booking.scheduled.lead_time_exceeded",
        "Scheduled reservation cannot be booked more than 7 days in advance (BR-01.a).");
}
