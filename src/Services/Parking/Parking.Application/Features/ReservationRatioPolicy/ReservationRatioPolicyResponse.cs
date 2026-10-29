using System;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy;

public sealed record ReservationRatioPolicyResponse(
    Guid LotId,
    int ReservationRatioPercent,
    bool IsConfigured,
    DateTime? UpdatedAtUtc = null);
