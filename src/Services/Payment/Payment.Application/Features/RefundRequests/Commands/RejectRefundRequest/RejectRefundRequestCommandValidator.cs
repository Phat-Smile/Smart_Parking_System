using FluentValidation;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;

public sealed class RejectRefundRequestCommandValidator : AbstractValidator<RejectRefundRequestCommand>
{
    public RejectRefundRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Refund request id is required.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin id is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}
