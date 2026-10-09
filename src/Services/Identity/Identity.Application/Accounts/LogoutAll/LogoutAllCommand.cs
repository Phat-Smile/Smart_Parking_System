using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts.Logout;

namespace ParkingSystem.Identity.Application.Accounts.LogoutAll;

public sealed record LogoutAllCommand(Guid AccountId) : IRequest<Result<LogoutResponse>>;

public sealed class LogoutAllCommandHandler(IAuthSessionManager authSessionManager)
    : IRequestHandler<LogoutAllCommand, Result<LogoutResponse>>
{
    public async Task<Result<LogoutResponse>> Handle(LogoutAllCommand request, CancellationToken cancellationToken)
    {
        await authSessionManager.RevokeAllSessionsAsync(request.AccountId, "LogoutAll", cancellationToken);

        return Result<LogoutResponse>.Success(new LogoutResponse("Đã đăng xuất thành công khỏi tất cả các thiết bị."));
    }
}
