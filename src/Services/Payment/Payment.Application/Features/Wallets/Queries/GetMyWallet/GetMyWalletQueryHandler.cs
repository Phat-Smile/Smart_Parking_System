using MediatR;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;

public class GetMyWalletQueryHandler : IRequestHandler<GetMyWalletQuery, Result<WalletBalanceDto>>
{
    private const string SandboxDemoNotice = "Wallet balance is for testing (sandbox) credit only, not real currency, cannot be redeemed for cash, cannot be withdrawn or transferred, and is not linked to any bank account.";

    private readonly IWalletRepository _walletRepository;
    private readonly WalletOptions _walletOptions;
    private readonly VnpayOptions _vnpayOptions;

    public GetMyWalletQueryHandler(
        IWalletRepository walletRepository,
        IOptions<WalletOptions> walletOptions,
        IOptions<VnpayOptions> vnpayOptions)
    {
        _walletRepository = walletRepository;
        _walletOptions = walletOptions.Value;
        _vnpayOptions = vnpayOptions.Value;
    }

    public async Task<Result<WalletBalanceDto>> Handle(
        GetMyWalletQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<WalletBalanceDto>.Failure(new Error("payment.invalid_user", "Invalid user identification in request."));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        var limits = new WalletLimitsDto
        {
            MinTopUp = _walletOptions.MinTopUpVnd,
            MaxTopUp = _walletOptions.MaxTopUpVnd,
            MaxBalance = _walletOptions.MaxBalanceVnd
        };

        if (wallet == null)
        {
            var emptyDto = new WalletBalanceDto
            {
                WalletId = null,
                Balance = 0m,
                Currency = "VND",
                IsDemo = true,
                DemoNotice = SandboxDemoNotice,
                Limits = limits,
                GatewayMode = _vnpayOptions.Mode,
                PendingTopUps = new List<PendingTopUpDto>()
            };

            return Result<WalletBalanceDto>.Success(emptyDto);
        }
        else
        {
            var activeDto = new WalletBalanceDto
            {
                WalletId = wallet.Id,
                Balance = wallet.Balance,
                Currency = "VND",
                IsDemo = true,
                DemoNotice = SandboxDemoNotice,
                Limits = limits,
                GatewayMode = _vnpayOptions.Mode,
                PendingTopUps = new List<PendingTopUpDto>()
            };

            return Result<WalletBalanceDto>.Success(activeDto);
        }
    }
}
