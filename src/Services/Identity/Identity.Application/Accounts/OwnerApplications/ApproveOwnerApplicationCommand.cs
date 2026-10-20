using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ApproveOwnerApplicationCommand(
    Guid AdminId,
    Guid AccountId) : IRequest<Result<OwnerApplicationActionResult>>;

public sealed class ApproveOwnerApplicationCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<ApproveOwnerApplicationCommand, Result<OwnerApplicationActionResult>>
{
    public async Task<Result<OwnerApplicationActionResult>> Handle(ApproveOwnerApplicationCommand request, CancellationToken cancellationToken)
    {
        if (request.AdminId == request.AccountId)
            return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.CannotApproveOwnApplication);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null || account.OwnerApplicationStatus is null)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.ApplicationNotFound);

            if (account.OwnerApplicationStatus != OwnerApplicationStatus.Pending)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.NotPending);

            var now = clock.UtcNow;
            account.ApproveOwnerApplication(request.AdminId, now);

            // Revoke all sessions and refresh tokens so user is forced to re-login to obtain new JWT with ParkingLotOwner role
            await authSessions.RevokeAllForAccountAsync(account.Id, now, "OwnerApplicationApproved", cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, "OwnerApplicationApproved", cancellationToken);

            outboxWriter.Enqueue(
                new OwnerApplicationApprovedIntegrationEvent(account.Id, account.Email, now),
                "identity.owner-application-approved.v1",
                1);

            await accounts.SaveChangesAsync(cancellationToken);

            return Result<OwnerApplicationActionResult>.Success(new OwnerApplicationActionResult(
                account.Id,
                account.OwnerApplicationStatus.Value.ToString(),
                "Hồ sơ chủ bãi đã được phê duyệt thành công. Người dùng cần đăng nhập lại để nhận quyền Chủ bãi."));
        }, cancellationToken);
    }
}
