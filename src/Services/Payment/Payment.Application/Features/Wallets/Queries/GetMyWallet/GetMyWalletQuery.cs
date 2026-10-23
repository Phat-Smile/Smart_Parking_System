using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;

public class GetMyWalletQuery : IRequest<Result<WalletBalanceDto>>
{
    public Guid UserId { get; }

    public GetMyWalletQuery(Guid userId)
    {
        UserId = userId;
    }
}
