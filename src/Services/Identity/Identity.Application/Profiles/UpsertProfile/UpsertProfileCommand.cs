using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Profiles.UpsertProfile;

public sealed record UpsertProfileCommand(
    Guid AccountId,
    string FullName,
    DateOnly? DateOfBirth,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    string? AvatarUrl) : IRequest<Result<ProfileResult>>;

public sealed record ProfileResult(
    Guid AccountId,
    string FullName,
    DateOnly? DateOfBirth,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    string? AvatarUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public static class UpsertProfileErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
}

public sealed class UpsertProfileCommandValidator : AbstractValidator<UpsertProfileCommand>
{
    public UpsertProfileCommandValidator()
    {
        RuleFor(c => c.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(Profile.MaxFullNameLength).WithMessage($"Họ và tên không được vượt quá {Profile.MaxFullNameLength} ký tự.");

        RuleFor(c => c.AvatarUrl)
            .MaximumLength(500).When(c => c.AvatarUrl is not null)
            .WithMessage("URL ảnh đại diện không được vượt quá 500 ký tự.");

        RuleFor(c => c.CitizenIdNumber)
            .MaximumLength(20).When(c => c.CitizenIdNumber is not null)
            .WithMessage("Số CCCD/CMND không được vượt quá 20 ký tự.");

        RuleFor(c => c.DriverLicenseNumber)
            .MaximumLength(20).When(c => c.DriverLicenseNumber is not null)
            .WithMessage("Số bằng lái xe không được vượt quá 20 ký tự.");
    }
}

public sealed class UpsertProfileCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    IClock clock) : IRequestHandler<UpsertProfileCommand, Result<ProfileResult>>
{
    public async Task<Result<ProfileResult>> Handle(UpsertProfileCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<ProfileResult>.Failure(UpsertProfileErrors.AccountNotFound);

        var now = clock.UtcNow;
        var profile = await profiles.FindByAccountIdAsync(request.AccountId, cancellationToken);

        if (profile is null)
        {
            profile = Profile.Create(
                request.AccountId,
                request.FullName,
                now,
                request.DateOfBirth,
                request.CitizenIdNumber,
                request.DriverLicenseNumber,
                request.AvatarUrl);
            profiles.Add(profile);
        }
        else
        {
            profile.Update(
                request.FullName,
                now,
                request.DateOfBirth,
                request.CitizenIdNumber,
                request.DriverLicenseNumber,
                request.AvatarUrl);
        }

        await profiles.SaveChangesAsync(cancellationToken);

        return Result<ProfileResult>.Success(new ProfileResult(
            profile.AccountId,
            profile.FullName,
            profile.DateOfBirth,
            profile.CitizenIdNumber,
            profile.DriverLicenseNumber,
            profile.AvatarUrl,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc));
    }
}
