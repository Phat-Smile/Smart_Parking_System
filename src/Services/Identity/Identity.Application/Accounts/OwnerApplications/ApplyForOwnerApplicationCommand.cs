using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record ApplyForOwnerApplicationCommand(
    Guid AccountId,
    string? TaxCode = null,
    string? BusinessName = null,
    string? PhoneNumber = null,
    string? FullName = null) : IRequest<Result<OwnerApplicationActionResult>>;

public sealed class ApplyForOwnerApplicationCommandValidator : AbstractValidator<ApplyForOwnerApplicationCommand>
{
    public ApplyForOwnerApplicationCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Mã tài khoản không được để trống.");

        RuleFor(x => x.TaxCode)
            .MaximumLength(32).WithMessage("Mã số thuế không được vượt quá 32 ký tự.")
            .Matches(@"^[0-9]{10}(-[0-9]{3})?$").WithMessage("Mã số thuế phải gồm 10 chữ số hoặc 13 chữ số dạng xxxxxxxxxx-xxx.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode));

        RuleFor(x => x.BusinessName)
            .MaximumLength(200).WithMessage("Tên doanh nghiệp/hộ kinh doanh không được vượt quá 200 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.BusinessName));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\s*(0|\+84)[35789][0-9]{8}\s*$")
            .WithMessage("Số điện thoại phải là số di động Việt Nam hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.FullName)
            .Must(name => name!.Trim().Length >= 2).WithMessage("Họ và tên phải có ít nhất 2 ký tự.")
            .Must(name => name!.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"Họ và tên không được vượt quá {Profile.MaxFullNameLength} ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));
    }
}

public sealed class ApplyForOwnerApplicationCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    IClock clock) : IRequestHandler<ApplyForOwnerApplicationCommand, Result<OwnerApplicationActionResult>>
{
    public async Task<Result<OwnerApplicationActionResult>> Handle(ApplyForOwnerApplicationCommand request, CancellationToken cancellationToken)
    {
        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.AccountNotFound);

            if (account.Role == AccountRole.ParkingLotOwner || account.OwnerApplicationStatus == OwnerApplicationStatus.Approved)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.AlreadyApproved);

            if (account.OwnerApplicationStatus == OwnerApplicationStatus.Pending)
                return Result<OwnerApplicationActionResult>.Failure(OwnerApplicationErrors.AlreadyPending);

            var now = clock.UtcNow;

            account.ApplyForParkingLotOwner(now, request.TaxCode, request.BusinessName);

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                account.UpdatePhoneNumber(request.PhoneNumber);
            }

            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                var profile = await profiles.FindByAccountIdAsync(account.Id, cancellationToken);
                if (profile is null)
                {
                    profiles.Add(Profile.Create(account.Id, request.FullName, now));
                }
                else
                {
                    profile.Update(
                        request.FullName,
                        now,
                        profile.DateOfBirth,
                        profile.CitizenIdNumber,
                        profile.DriverLicenseNumber,
                        profile.AvatarUrl);
                }
            }

            await accounts.SaveChangesAsync(cancellationToken);

            return Result<OwnerApplicationActionResult>.Success(new OwnerApplicationActionResult(
                account.Id,
                account.OwnerApplicationStatus!.Value.ToString(),
                "Yêu cầu đăng ký xét duyệt chủ bãi đã được gửi thành công. Vui lòng chờ quản trị viên phê duyệt."));
        }, cancellationToken);
    }
}
