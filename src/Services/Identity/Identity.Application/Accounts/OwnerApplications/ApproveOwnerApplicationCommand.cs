using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ApproveOwnerApplicationCommand(
    Guid AdminId,
    Guid AccountId,
    string? IpAddress = null) : IRequest<Result<OwnerApplicationActionResult>>;

public sealed class ApproveOwnerApplicationCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IOwnerDocumentRepository documents,
    IOwnerDocumentRequirements requirements,
    IOutboxWriter outboxWriter,
    IClock clock,
    IIdentityAuditLog? auditLog = null) : IRequestHandler<ApproveOwnerApplicationCommand, Result<OwnerApplicationActionResult>>
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

            // The role is granted only for a complete checklist of the owner type the applicant chose. The row lock taken above queues
            // an upload or a delete behind this decision, so the documents cannot change between the check and the approval.
            if (account.OwnerType is not { } ownerType)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.OwnerTypeRequired);

            // A stored number that is not an owner type has no checklist, and the lookup throws for it.
            if (!Enum.IsDefined(ownerType))
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.OwnerTypeInvalid);

            var uploaded = await documents.ListByAccountAsync(account.Id, cancellationToken);
            var missing = requirements.GetMissingRequiredDocuments(ownerType, uploaded.Select(document => document.DocumentType));
            if (missing.Count > 0)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.DocumentsIncomplete);

            var now = clock.UtcNow;
            account.ApproveOwnerApplication(request.AdminId, now);

            // Revoke all sessions and refresh tokens so user is forced to re-login to obtain new JWT with ParkingLotOwner role
            await authSessions.RevokeAllForAccountAsync(account.Id, now, "OwnerApplicationApproved", cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, "OwnerApplicationApproved", cancellationToken);

            auditLog?.Record(
                actionType: "account.owner_approved",
                actorId: request.AdminId,
                actorEmail: null,
                actorRole: "SystemAdmin",
                targetType: "account",
                targetId: account.Id.ToString(),
                reason: "Phê duyệt hồ sơ đăng ký Chủ bãi",
                ipAddress: request.IpAddress);

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
