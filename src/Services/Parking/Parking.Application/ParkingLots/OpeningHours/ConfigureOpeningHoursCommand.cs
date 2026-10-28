using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;

namespace ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

public sealed record ConfigureOpeningHoursCommand(Guid LotId, Guid OwnerAccountId, WeeklyOpeningHours? Schedule)
    : IRequest<Result<OpeningHoursView>>;

public static class OpeningHoursErrors
{
    // Deliberately do not disclose whether another owner's lot exists.
    public static readonly Error LotNotFoundOrNotOwned = new(
        "parking.opening_hours.lot_not_found", "The parking lot does not exist or you do not own it.");
}

public sealed class ConfigureOpeningHoursCommandValidator : AbstractValidator<ConfigureOpeningHoursCommand>
{
    public ConfigureOpeningHoursCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.OwnerAccountId).NotEmpty();
        RuleFor(command => command.Schedule)
            .Must(OpeningHoursSchedule.IsValid)
            .WithMessage("Provide all seven unique days (0=Sunday to 6=Saturday). Closed/24-hour days have no times; otherwise use HH:mm with different opening and closing times.");
    }
}

public sealed class ConfigureOpeningHoursCommandHandler(
    IParkingLotOpeningHoursRepository parkingLots,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ConfigureOpeningHoursCommand, Result<OpeningHoursView>>
{
    public async Task<Result<OpeningHoursView>> Handle(ConfigureOpeningHoursCommand command, CancellationToken cancellationToken)
    {
        var lot = await parkingLots.FindForOwnerAsync(command.LotId, command.OwnerAccountId, cancellationToken);
        if (lot is null)
            return Result<OpeningHoursView>.Failure(OpeningHoursErrors.LotNotFoundOrNotOwned);

        // ValidationBehavior checks the schedule before the handler runs.
        var json = OpeningHoursSchedule.Serialize(command.Schedule!);
        // PostgreSQL jsonb normalizes object-key order; compare the parsed schedule, not raw JSON text.
        WeeklyOpeningHours? previous = null;
        try
        {
            previous = OpeningHoursSchedule.Deserialize(lot.OpeningHoursJson);
        }
        catch (System.Text.Json.JsonException)
        {
            // Invalid legacy JSON is repaired by writing the validated new schedule.
        }
        if (previous is not null && OpeningHoursSchedule.IsValid(previous) &&
            previous.Days.OrderBy(day => day.DayOfWeek)
                .SequenceEqual(command.Schedule!.Days.OrderBy(day => day.DayOfWeek)))
            return Result<OpeningHoursView>.Success(new OpeningHoursView(lot.Id, true, command.Schedule));

        lot.SetOpeningHours(json);
        auditLog.Record(new AuditEntry(
            AuditActions.ParkingLotOpeningHoursUpdated,
            command.OwnerAccountId,
            lot.Id,
            nameof(ParkingSystem.Parking.Domain.ParkingLots.ParkingLot),
            lot.Id.ToString(),
            Reason: null,
            clock.UtcNow));
        await parkingLots.SaveChangesAsync(cancellationToken);
        return Result<OpeningHoursView>.Success(new OpeningHoursView(lot.Id, true, command.Schedule));
    }
}

public sealed record GetOpeningHoursQuery(Guid LotId, Guid? OwnerAccountId = null) : IRequest<OpeningHoursView?>;

public sealed class GetOpeningHoursQueryHandler(IParkingLotOpeningHoursRepository parkingLots)
    : IRequestHandler<GetOpeningHoursQuery, OpeningHoursView?>
{
    public async Task<OpeningHoursView?> Handle(GetOpeningHoursQuery request, CancellationToken cancellationToken)
    {
        var lot = request.OwnerAccountId is Guid ownerId
            ? await parkingLots.FindForOwnerAsync(request.LotId, ownerId, cancellationToken)
            : await parkingLots.FindApprovedAsync(request.LotId, cancellationToken);
        if (lot is null)
            return null;
        var schedule = OpeningHoursSchedule.Deserialize(lot.OpeningHoursJson);
        return new OpeningHoursView(lot.Id, schedule is not null, schedule);
    }
}
