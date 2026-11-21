using FluentValidation;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;

public sealed class CreateRefundRequestCommandValidator : AbstractValidator<CreateRefundRequestCommand>
{
    public CreateRefundRequestCommandValidator()
    {
        RuleFor(x => x.CallerId)
            .NotEmpty().WithMessage("CallerId must not be empty.");

        RuleFor(x => x.LotId)
            .NotEmpty().WithMessage("Parking lot id is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .Must(a => a == Math.Truncate(a)).WithMessage("Amount must be a whole number in VND.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");

        RuleFor(x => x.ReferenceId)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.ReferenceId))
            .WithMessage("ReferenceId must not exceed 100 characters.");
    }
}
