using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;

public record CreateWalletTopupCommand(
    Guid UserId,
    decimal Amount,
    string IdempotencyKey,
    string ClientIp
) : IRequest<Result<WalletTopupResultDto>>;
