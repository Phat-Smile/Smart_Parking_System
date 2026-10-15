using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles;

namespace ParkingSystem.Identity.Application.Accounts.GetMe;

public sealed record GetMeQuery(Guid AccountId) : IRequest<Result<MeResult>>;

public sealed record MeResult(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string? FullName,
    string Role,
    string Status,
    bool HasPassword,
    bool GoogleLinked,
    DateTimeOffset CreatedAtUtc);

public static class GetMeErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
}

public sealed class GetMeQueryHandler(
    IAccountRepository accounts,
    IProfileRepository profiles) : IRequestHandler<GetMeQuery, Result<MeResult>>
{
    public async Task<Result<MeResult>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<MeResult>.Failure(GetMeErrors.AccountNotFound);

        var profile = await profiles.FindByAccountIdAsync(request.AccountId, cancellationToken);

        return Result<MeResult>.Success(new MeResult(
            account.Id,
            account.Email,
            account.PhoneNumber,
            profile?.FullName,
            account.Role.ToString(),
            account.Status.ToString(),
            account.HasPassword,
            account.GoogleSubject is not null,
            account.CreatedAtUtc));
    }
}
