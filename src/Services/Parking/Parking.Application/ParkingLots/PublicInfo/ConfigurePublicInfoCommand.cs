using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Parking.Application.ParkingLots.PublicInfo;

public sealed record ConfigurePublicInfoCommand(Guid LotId, Guid OwnerAccountId, ParkingLotPublicInfo Info)
    : IRequest<Result<ParkingLotPublicInfo>>;

public interface IParkingLotPublicInfoRepository
{
    Task<ParkingLot?> FindForOwnerAsync(Guid lotId, Guid ownerId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class ConfigurePublicInfoCommandValidator : AbstractValidator<ConfigurePublicInfoCommand>
{
    public ConfigurePublicInfoCommandValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.OwnerAccountId).NotEmpty();
        RuleFor(x => x.Info).NotNull();
        RuleFor(x => x.Info).Must(x => x is null || x.Description is null || x.Description.Length <= 1200)
            .WithMessage("Description must be at most 1200 characters.");
        RuleFor(x => x.Info).Must(x => x is null || x.EntryInstructions is null || x.EntryInstructions.Length <= 800)
            .WithMessage("Entrance instructions must be at most 800 characters.");
        RuleFor(x => x.Info).Must(x => x is null || x.EvChargingPoints is null
            || x.EvChargingPoints is >= 0 and <= 1000)
            .WithMessage("EV charging points must be between 0 and 1000.");
        RuleFor(x => x.Info).Must(x => x is not null && x.Rules is not null && x.Rules.Count <= 10
                && x.Rules.All(rule => !string.IsNullOrWhiteSpace(rule) && rule.Length <= 200))
            .WithMessage("Provide at most 10 rules, each 1-200 characters.");
        RuleFor(x => x.Info).Must(x => x is not null && x.PaymentMethods is not null
                && x.PaymentMethods.Count <= 8
                && x.PaymentMethods.All(method => !string.IsNullOrWhiteSpace(method) && method.Length <= 40))
            .WithMessage("Provide at most 8 payment methods, each 1-40 characters.");
        RuleFor(x => x.Info).Must(x => x is not null && x.PhotoUrls is not null && x.PhotoUrls.Count <= 8
                && x.PhotoUrls.All(url => url.StartsWith("/images/parkwise/", StringComparison.Ordinal)
                    && !url.Contains("..", StringComparison.Ordinal) && url.Length <= 200))
            .WithMessage("Photos must reference 0-8 pre-published /images/parkwise/ assets.");
    }
}

public sealed class ConfigurePublicInfoCommandHandler(IParkingLotPublicInfoRepository repo, IAuditLog audit, IClock clock)
    : IRequestHandler<ConfigurePublicInfoCommand, Result<ParkingLotPublicInfo>>
{
    public async Task<Result<ParkingLotPublicInfo>> Handle(ConfigurePublicInfoCommand request, CancellationToken ct)
    {
        var lot = await repo.FindForOwnerAsync(request.LotId, request.OwnerAccountId, ct);
        if (lot is null)
            return Result<ParkingLotPublicInfo>.Failure(new Error("parking.public_info.not_found",
                "Parking lot not found or caller does not own it."));
        lot.SetPublicInfo(request.Info);
        audit.Record(new AuditEntry(AuditActions.ParkingLotPublicInfoUpdated,
            request.OwnerAccountId, lot.Id, nameof(ParkingLot), lot.Id.ToString(), null, clock.UtcNow));
        await repo.SaveChangesAsync(ct);
        return Result<ParkingLotPublicInfo>.Success(request.Info);
    }
}
