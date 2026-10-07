using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Application.Accounts.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result<LogoutResponse>>;

public sealed record LogoutResponse(string Message = "Logged out successfully.");

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.");
    }
}

public sealed class LogoutCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRequestHandler<LogoutCommand, Result<LogoutResponse>>
{
    public async Task<Result<LogoutResponse>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = refreshTokenGenerator.HashToken(request.RefreshToken);

        var token = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);
        if (token is not null)
        {
            await accounts.ExecuteInTransactionAsync(async () =>
            {
                var session = await authSessions.FindByIdForUpdateAsync(token.SessionId, cancellationToken);
                if (session is not null && !session.IsRevoked)
                {
                    session.Revoke(now, "Logout");
                }

                token.Revoke(now, "Logout");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(token.SessionId, now, "Logout", cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }

        return Result<LogoutResponse>.Success(new LogoutResponse("Logged out successfully."));
    }
}
