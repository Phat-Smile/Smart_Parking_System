using FluentValidation;
using ParkingSystem.Parking.Application.Common.Constants;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;

public sealed class ConfigureDepositPolicyCommandValidator : AbstractValidator<ConfigureDepositPolicyCommand>
{
    public ConfigureDepositPolicyCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.LotId)
            .NotEmpty()
            .WithMessage(ParkingErrors.DepositPolicy.LotIdEmpty.Message);

        RuleFor(x => x.HoldDepositVnd)
            .NotNull()
            .WithMessage(ParkingErrors.DepositPolicy.DepositRequired.Message)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(ParkingErrors.DepositPolicy.DepositNegative.Message)
            .Must(amount => amount!.Value % 1m == 0m)
            .WithMessage(ParkingErrors.DepositPolicy.DepositMustBeInteger.Message);
    }
}
