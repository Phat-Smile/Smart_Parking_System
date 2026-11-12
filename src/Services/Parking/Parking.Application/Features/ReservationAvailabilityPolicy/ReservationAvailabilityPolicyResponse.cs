using System;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy;

public sealed record ReservationAvailabilityPolicyResponse(
    Guid LotId,
    bool ReservationEnabled,
    bool IsConfigured,
    DateTime? UpdatedAtUtc);
