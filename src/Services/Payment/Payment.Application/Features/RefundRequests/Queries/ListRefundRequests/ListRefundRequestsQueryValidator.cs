using FluentValidation;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;

public sealed class ListRefundRequestsQueryValidator : AbstractValidator<ListRefundRequestsQuery>
{
    public ListRefundRequestsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be a whole number of 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
