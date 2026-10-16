using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.WebAPI.Common;

namespace ParkingSystem.Payment.WebAPI.Controllers;

[Authorize]
[Route("api/payment/wallets")]
public class WalletsController : ApiControllerBase
{
    private readonly ISender _sender;

    public WalletsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<WalletBalanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        var isValidUser = TryGetCurrentUserId(out var userId, out var errorResult);
        if (isValidUser == false)
        {
            return errorResult!;
        }

        var query = new GetMyWalletQuery(userId);
        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result, "Get wallet successfully");
    }
}
