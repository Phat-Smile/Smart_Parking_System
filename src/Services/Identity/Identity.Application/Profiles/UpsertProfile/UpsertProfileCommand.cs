using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Application.Profiles.CustomerMedia;

namespace ParkingSystem.Identity.Application.Profiles.UpsertProfile;

public sealed record UpsertProfileCommand(
    Guid AccountId,
    string FullName,
    DateOnly? DateOfBirth,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    string? AvatarUrl,
    string? PhoneNumber = null,
    string? Address = null,
    string? IdentityDocumentType = null,
    DateOnly? IdentityIssueDate = null,
    string? IdentityIssuedBy = null,
    Guid? IdentityFrontMediaId = null,
    Guid? IdentityBackMediaId = null) : IRequest<Result<ProfileResult>>;

public sealed record ProfileResult(
    Guid AccountId,
    string FullName,
    DateOnly? DateOfBirth,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    string? AvatarUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string? PhoneNumber = null,
    string? Address = null,
    string? IdentityDocumentType = null,
    DateOnly? IdentityIssueDate = null,
    string? IdentityIssuedBy = null,
    Guid? IdentityFrontMediaId = null,
    Guid? IdentityBackMediaId = null);

public static class UpsertProfileErrors
{
    public static readonly Error InvalidIdentityDocument =
        new("identity.invalid_profile_document", "Identity document is incomplete or does not belong to this account.");
    public static readonly Error InvalidAvatar =
        new("identity.invalid_avatar", "Avatar media does not belong to this account.");
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");
    public static readonly Error DuplicatePhone =
        new("identity.duplicate_phone", "Số điện thoại đã được sử dụng.");
}

public sealed class UpsertProfileCommandValidator : AbstractValidator<UpsertProfileCommand>
{
    private const int MaxAgeYears = 120;

    public UpsertProfileCommandValidator(IClock clock)
    {
        RuleFor(c => c.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(Profile.MaxFullNameLength).WithMessage($"Họ và tên không được vượt quá {Profile.MaxFullNameLength} ký tự.");

        RuleFor(c => c.DateOfBirth)
            .Must(dob => dob!.Value <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
            .WithMessage("Ngày sinh không được ở tương lai.")
            .Must(dob => dob!.Value >= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-MaxAgeYears))
            .WithMessage($"Ngày sinh không hợp lệ (quá {MaxAgeYears} năm trước).")
            .When(c => c.DateOfBirth is not null);

        RuleFor(c => c.AvatarUrl)
            .MaximumLength(500).When(c => c.AvatarUrl is not null)
            .WithMessage("URL ảnh đại diện không được vượt quá 500 ký tự.");

        RuleFor(c => c.AvatarUrl)
            .Must(url => (Uri.TryCreate(url!.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                || (url!.StartsWith("/api/identity/accounts/me/media/", StringComparison.Ordinal)
                    && Guid.TryParse(url["/api/identity/accounts/me/media/".Length..], out _)))
            .When(c => !string.IsNullOrWhiteSpace(c.AvatarUrl))
            .WithMessage("URL ảnh đại diện phải là địa chỉ http hoặc https đầy đủ.");

        RuleFor(c => c.CitizenIdNumber)
            .MaximumLength(20).When(c => c.CitizenIdNumber is not null)
            .WithMessage("Số CCCD/CMND không được vượt quá 20 ký tự.");

        RuleFor(c => c.DriverLicenseNumber)
            .MaximumLength(20).When(c => c.DriverLicenseNumber is not null)
            .WithMessage("Số bằng lái xe không được vượt quá 20 ký tự.");

        RuleFor(c => c.PhoneNumber)
            .Matches("^(0[0-9]{9}|\\+84[0-9]{9})$")
            .When(c => !string.IsNullOrWhiteSpace(c.PhoneNumber));
        RuleFor(c => c.Address)
            .MaximumLength(500).When(c => c.Address is not null);
        RuleFor(c => c.IdentityDocumentType).Must(type => type is null or "citizenId" or "driverLicense");
        RuleFor(c => c.IdentityIssueDate).Must(date => date.HasValue && date.Value <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
            .When(c => c.IdentityDocumentType is not null);
        RuleFor(c => c.IdentityIssuedBy).NotEmpty().MaximumLength(150)
            .When(c => c.IdentityDocumentType is not null);
        RuleFor(c => c.IdentityFrontMediaId).NotEmpty().When(c => c.IdentityDocumentType is not null);
        RuleFor(c => c.IdentityBackMediaId).NotEmpty().When(c => c.IdentityDocumentType is not null);
        RuleFor(c => c).Must(c => c.IdentityDocumentType is null ||
            (c.IdentityDocumentType == "citizenId" ? !string.IsNullOrWhiteSpace(c.CitizenIdNumber)
                : !string.IsNullOrWhiteSpace(c.DriverLicenseNumber)));
    }
}

public sealed class UpsertProfileCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    ICustomerMediaStore media,
    IClock clock) : IRequestHandler<UpsertProfileCommand, Result<ProfileResult>>
{
    public async Task<Result<ProfileResult>> Handle(UpsertProfileCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<ProfileResult>.Failure(UpsertProfileErrors.AccountNotFound);

        if (request.AvatarUrl is { } avatar && avatar.StartsWith("/api/identity/accounts/me/media/", StringComparison.Ordinal))
        {
            var idText = avatar["/api/identity/accounts/me/media/".Length..];
            if (!Guid.TryParse(idText, out var avatarId) ||
                !await media.ExistsAsync(request.AccountId, avatarId, "avatar", cancellationToken))
                return Result<ProfileResult>.Failure(UpsertProfileErrors.InvalidAvatar);
        }
        if (request.IdentityDocumentType is not null)
        {
            if (!request.IdentityFrontMediaId.HasValue || !request.IdentityBackMediaId.HasValue ||
                !await media.ExistsAsync(request.AccountId, request.IdentityFrontMediaId.Value, "identity-front", cancellationToken) ||
                !await media.ExistsAsync(request.AccountId, request.IdentityBackMediaId.Value, "identity-back", cancellationToken))
                return Result<ProfileResult>.Failure(UpsertProfileErrors.InvalidIdentityDocument);
        }

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
                request.AvatarUrl,
                request.Address);
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
                request.AvatarUrl,
                request.Address ?? profile.Address);
        }

        if (request.PhoneNumber is not null)
            account.UpdatePhoneNumber(request.PhoneNumber);
        if (request.IdentityDocumentType is not null)
            profile.SetIdentityDocument(request.IdentityDocumentType, request.IdentityIssueDate!.Value,
                request.IdentityIssuedBy!, request.IdentityFrontMediaId!.Value, request.IdentityBackMediaId!.Value, now);
        else if ((profile.IdentityDocumentType == "citizenId" && profile.CitizenIdNumber is null) ||
            (profile.IdentityDocumentType == "driverLicense" && profile.DriverLicenseNumber is null))
            profile.ClearIdentity(now);

        if (!await accounts.TrySaveChangesAsync(cancellationToken))
            return Result<ProfileResult>.Failure(UpsertProfileErrors.DuplicatePhone);

        return Result<ProfileResult>.Success(new ProfileResult(
            profile.AccountId,
            profile.FullName,
            profile.DateOfBirth,
            IdentityMaskingHelper.MaskCitizenId(profile.CitizenIdNumber),
            IdentityMaskingHelper.MaskDriverLicense(profile.DriverLicenseNumber),
            profile.AvatarUrl,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            account.PhoneNumber,
            profile.Address,
            profile.IdentityDocumentType,
            profile.IdentityIssueDate,
            profile.IdentityIssuedBy,
            profile.IdentityFrontMediaId,
            profile.IdentityBackMediaId));
    }
}
