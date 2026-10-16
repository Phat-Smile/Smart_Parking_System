using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Profiles;

namespace ParkingSystem.Identity.Application.Accounts.GetCurrentAccount;

// FE gọi sau mỗi lần tải lại trang để dựng menu theo role; AccountId luôn lấy từ token, không nhận từ client.
public sealed record GetCurrentAccountQuery(Guid AccountId) : IRequest<Result<CurrentAccount>>;

public sealed record CurrentAccount(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string? FullName,
    string Role,
    string Status,
    bool HasPassword,
    bool GoogleLinked,
    DateTimeOffset CreatedAtUtc);

public sealed class GetCurrentAccountQueryHandler(IAccountRepository accounts, IProfileRepository profiles)
    : IRequestHandler<GetCurrentAccountQuery, Result<CurrentAccount>>
{
    public async Task<Result<CurrentAccount>> Handle(GetCurrentAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<CurrentAccount>.Failure(UnlockAccountErrors.AccountNotFound);

        // Họ tên nằm ở Profile, có thể chưa có vì đăng ký không bắt buộc nhập.
        var profile = await profiles.FindByAccountIdAsync(account.Id, cancellationToken);

        return Result<CurrentAccount>.Success(new CurrentAccount(
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
