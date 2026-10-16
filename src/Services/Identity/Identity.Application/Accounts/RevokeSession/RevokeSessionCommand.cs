using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Identity.Application.Accounts.RevokeSession;

public sealed record RevokeSessionCommand(Guid AccountId, Guid SessionId) : IRequest<Result<RevokedSession>>;

public sealed record RevokedSession(Guid SessionId, string Message);

public static class RevokeSessionErrors
{
    public static readonly Error SessionNotFound =
        new("identity.session_not_found", "Phiên làm việc không tồn tại hoặc đã bị thu hồi.");
}

public sealed class RevokeSessionCommandValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty().WithMessage("AccountId là bắt buộc.");
        RuleFor(c => c.SessionId).NotEmpty().WithMessage("SessionId là bắt buộc.");
    }
}

public sealed class RevokeSessionCommandHandler(
    IAuthSessionRepository authSessions,
    IAccountRepository accounts,
    IClock clock) : IRequestHandler<RevokeSessionCommand, Result<RevokedSession>>
{
    public async Task<Result<RevokedSession>> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var session = await authSessions.FindByIdForUpdateAsync(request.SessionId, cancellationToken);

            if (session is null || session.AccountId != request.AccountId || session.IsRevoked)
                return Result<RevokedSession>.Failure(RevokeSessionErrors.SessionNotFound);

            session.Revoke(now, "RevokedByUser");
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<RevokedSession>.Success(
                new RevokedSession(session.Id, "Phiên làm việc đã được thu hồi."));
        }, cancellationToken);
    }
}
