using System;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy;

public sealed record DepositPolicyResponse(
    Guid LotId,
    decimal? HoldDepositVnd,
    bool IsConfigured);
