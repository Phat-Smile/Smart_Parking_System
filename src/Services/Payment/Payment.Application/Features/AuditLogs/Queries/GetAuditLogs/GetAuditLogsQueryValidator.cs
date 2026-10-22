using FluentValidation;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(AuditLogErrors.InvalidPage.Message);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage(AuditLogErrors.InvalidPageSize.Message);

        RuleFor(x => x.ActionType)
            .Must(action => string.IsNullOrWhiteSpace(action) || AuditActionCatalog.IsValid(action))
            .WithMessage(AuditLogErrors.InvalidActionType.Message);

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc.Value < x.ToUtc.Value)
            .WithMessage(AuditLogErrors.InvalidDateRange.Message);
    }
}
