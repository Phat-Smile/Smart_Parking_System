using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;

public sealed record RegisterParkingLotCommand(
    Guid OwnerUserId,
    string Name,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    string Type,
    ParkingLotFeatures? Features = null) : IRequest<RegisteredParkingLot>;

public sealed record RegisteredParkingLot(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus);

public sealed class RegisterParkingLotCommandValidator : AbstractValidator<RegisterParkingLotCommand>
{
    public RegisterParkingLotCommandValidator()
    {
        RuleFor(command => command.OwnerUserId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= ParkingLot.NameMaxLength)
            .WithMessage($"Name must contain 1-{ParkingLot.NameMaxLength} characters.");
        RuleFor(command => command.Address)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= ParkingLot.AddressMaxLength)
            .WithMessage($"Address must contain 1-{ParkingLot.AddressMaxLength} characters.");
        RuleFor(command => command.Latitude)
            .Must(value => value.HasValue && value.Value is >= -90m and <= 90m)
            .WithMessage("Latitude is required and must be between -90 and 90.");
        RuleFor(command => command.Longitude)
            .Must(value => value.HasValue && value.Value is >= -180m and <= 180m)
            .WithMessage("Longitude is required and must be between -180 and 180.");
        RuleFor(command => command.Type)
            .Must(value => !string.IsNullOrWhiteSpace(value) &&
                Enum.GetNames<ParkingLotType>().Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Type must be one of: {string.Join(", ", Enum.GetNames<ParkingLotType>())}.");
    }
}

public sealed class RegisterParkingLotCommandHandler(IRegisterParkingLotRepository parkingLots, IClock clock)
    : IRequestHandler<RegisterParkingLotCommand, RegisteredParkingLot>
{
    public async Task<RegisteredParkingLot> Handle(
        RegisterParkingLotCommand request,
        CancellationToken cancellationToken)
    {
        // Fast rejection for repeated submissions. The database unique index is still
        // required because concurrent requests can both pass this check.
        if (await parkingLots.ExistsForOwnerAsync(request.OwnerUserId, request.Name.Trim(), cancellationToken))
            throw new DuplicateParkingLotException();

        var parkingLot = ParkingLot.Register(
            Guid.NewGuid(),
            request.OwnerUserId,
            request.Name,
            request.Address,
            request.Latitude!.Value,
            request.Longitude!.Value,
            Enum.Parse<ParkingLotType>(request.Type, ignoreCase: true));
        parkingLot.SetFeatures(request.Features ?? new ParkingLotFeatures());

        // MOD-PK-04: the owner of a lot is whoever holds its Owner assignment, so the registrant gets that row now.
        // owner_user_id stays as the record of who registered the lot; it is not used for authorization.
        var ownerAssignment = ParkingLotAssignment.Create(
            parkingLot.Id,
            request.OwnerUserId,
            ParkingLotMemberRole.Owner,
            request.OwnerUserId,
            clock.UtcNow);

        await parkingLots.AddAsync(parkingLot, ownerAssignment, cancellationToken);

        return new RegisteredParkingLot(
            parkingLot.Id,
            parkingLot.OwnerUserId!.Value,
            parkingLot.Name,
            parkingLot.Address,
            parkingLot.Latitude,
            parkingLot.Longitude,
            parkingLot.Type.ToString(),
            parkingLot.ApprovalStatus.ToString());
    }
}
