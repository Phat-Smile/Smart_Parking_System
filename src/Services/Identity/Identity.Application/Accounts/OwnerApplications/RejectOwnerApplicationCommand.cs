using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record RejectOwnerApplicationCommand(
    Guid AdminId,
    Guid AccountId,
    string Reason) : IRequest<Result<OwnerApplicationActionResult>>;

public sealed class RejectOwnerApplicationCommandValidator : AbstractValidator<RejectOwnerApplicationCommand>
{
    public RejectOwnerApplicationCommandValidator()
    {
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage(OwnerApplicationErrors.InvalidReason.Message)
            .MaximumLength(500).WithMessage(OwnerApplicationErrors.InvalidReason.Message);
    }
}

public sealed class RejectOwnerApplicationCommandHandler(
    IAccountRepository accounts,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<RejectOwnerApplicationCommand, Result<OwnerApplicationActionResult>>
{
    public async Task<Result<OwnerApplicationActionResult>> Handle(RejectOwnerApplicationCommand request, CancellationToken cancellationToken)
    {
        if (request.AdminId == request.AccountId)
            return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.CannotRejectOwnApplication);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null || account.OwnerApplicationStatus is null)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.ApplicationNotFound);

            if (account.OwnerApplicationStatus != OwnerApplicationStatus.Pending)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.NotPending);

            var now = clock.UtcNow;
            account.RejectOwnerApplication(request.AdminId, request.Reason, now);

            outboxWriter.Enqueue(
                new OwnerApplicationRejectedIntegrationEvent(account.Id, account.Email, request.Reason, now),
                "identity.owner-application-rejected.v1",
                1);

            await accounts.SaveChangesAsync(cancellationToken);

            return Result<OwnerApplicationActionResult>.Success(new OwnerApplicationActionResult(
                account.Id,
                account.OwnerApplicationStatus.Value.ToString(),
                "Hồ sơ chủ bãi đã bị từ chối."));
        }, cancellationToken);
    }
}
