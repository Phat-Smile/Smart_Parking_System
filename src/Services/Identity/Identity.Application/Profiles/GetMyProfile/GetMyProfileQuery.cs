using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles.UpsertProfile;

namespace ParkingSystem.Identity.Application.Profiles.GetMyProfile;

public sealed record GetMyProfileQuery(Guid AccountId) : IRequest<Result<ProfileResult>>;

public static class GetMyProfileErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
}

public sealed class GetMyProfileQueryHandler(
    IAccountRepository accounts,
    IProfileRepository profiles) : IRequestHandler<GetMyProfileQuery, Result<ProfileResult>>
{
    public async Task<Result<ProfileResult>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<ProfileResult>.Failure(GetMyProfileErrors.AccountNotFound);

        var profile = await profiles.FindByAccountIdAsync(request.AccountId, cancellationToken);
        if (profile is not null)
        {
            return Result<ProfileResult>.Success(new ProfileResult(
                profile.AccountId,
                profile.FullName,
                profile.DateOfBirth,
                IdentityMaskingHelper.MaskCitizenId(profile.CitizenIdNumber),
                IdentityMaskingHelper.MaskDriverLicense(profile.DriverLicenseNumber),
                profile.AvatarUrl,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc));
        }

        // Nếu người dùng mới đăng ký chưa cập nhật hồ sơ, trả về DTO mặc định an toàn cho Frontend
        return Result<ProfileResult>.Success(new ProfileResult(
            account.Id,
            account.Email.Split('@')[0],
            null,
            null,
            null,
            null,
            account.CreatedAtUtc,
            null));
    }
}
