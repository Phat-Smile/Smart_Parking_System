using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;

namespace ParkingSystem.Identity.Application.Profiles.DeleteProfileIdentity;

public sealed record DeleteProfileIdentityCommand(Guid AccountId) : IRequest<Result<DeletedProfileIdentityResult>>;

public sealed record DeletedProfileIdentityResult(string Message);

public static class DeleteProfileIdentityErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
}

public sealed class DeleteProfileIdentityCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    IClock clock) : IRequestHandler<DeleteProfileIdentityCommand, Result<DeletedProfileIdentityResult>>
{
    public async Task<Result<DeletedProfileIdentityResult>> Handle(
        DeleteProfileIdentityCommand request,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<DeletedProfileIdentityResult>.Failure(DeleteProfileIdentityErrors.AccountNotFound);

        var profile = await profiles.FindByAccountIdAsync(request.AccountId, cancellationToken);
        if (profile is not null)
        {
            profile.ClearIdentity(clock.UtcNow);
            await profiles.SaveChangesAsync(cancellationToken);
        }

        return Result<DeletedProfileIdentityResult>.Success(
            new DeletedProfileIdentityResult("Thông tin định danh (CCCD/GPLX) đã được xóa thành công."));
    }
}
