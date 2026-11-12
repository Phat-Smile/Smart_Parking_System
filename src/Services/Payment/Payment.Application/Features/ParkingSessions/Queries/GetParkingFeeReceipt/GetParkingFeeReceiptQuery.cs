using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;

public sealed record GetParkingFeeReceiptQuery(
    Guid Identifier,
    Guid? CallerAccountId,
    string CallerRole) : IRequest<Result<ParkingFeeReceiptResponse>>;
