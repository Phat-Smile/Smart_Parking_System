using FluentValidation;
using ParkingSystem.Parking.Application.Common.Constants;
using System.Linq;

namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingCommandValidator : AbstractValidator<ConfigurePricingCommand>
{
    private static readonly string[] ValidVehicleTypes = { "CAR", "MOTORBIKE" };
    private static readonly string[] ValidBillingModes = { "PerEntry", "Block" };
    private static readonly int[] ValidBlockMinutes = { 15, 30, 60 };

    public ConfigurePricingCommandValidator()
    {
        RuleFor(x => x.LotId)
            .NotEmpty()
            .WithMessage(ParkingErrors.Pricing.LotIdEmpty.Message);

        RuleFor(x => x.VehicleType)
            .NotEmpty()
            .WithMessage(ParkingErrors.Pricing.VehicleTypeEmpty.Message)
            .Must(type => ValidVehicleTypes.Contains(type.Trim().ToUpperInvariant()))
            .WithMessage(ParkingErrors.Pricing.VehicleTypeInvalid.Message);

        RuleFor(x => x.BillingMode)
            .NotEmpty()
            .WithMessage(ParkingErrors.Pricing.BillingModeInvalid.Message)
            .Must(mode => ValidBillingModes.Contains(mode))
            .WithMessage(ParkingErrors.Pricing.BillingModeInvalid.Message);

        When(x => x.BillingMode == "Block", () =>
        {
            RuleFor(x => x.BlockMinutes)
                .NotNull()
                .WithMessage(ParkingErrors.Pricing.BlockMinutesInvalid.Message)
                .Must(minutes => minutes.HasValue && ValidBlockMinutes.Contains(minutes.Value))
                .WithMessage(ParkingErrors.Pricing.BlockMinutesInvalid.Message);
        });

        When(x => x.BillingMode == "PerEntry", () =>
        {
            RuleFor(x => x.BlockMinutes)
                .Null()
                .WithMessage(ParkingErrors.Pricing.BlockMinutesInvalid.Message);
        });

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage(ParkingErrors.Pricing.PriceNegative.Message)
            .Must(price => price % 1m == 0m)
            .WithMessage(ParkingErrors.Pricing.PriceMustBeInteger.Message);

        RuleFor(x => x.OvernightFee)
            .GreaterThanOrEqualTo(0)
            .WithMessage(ParkingErrors.Pricing.OvernightFeeNegative.Message)
            .Must(fee => fee % 1m == 0m)
            .WithMessage(ParkingErrors.Pricing.OvernightFeeMustBeInteger.Message);

        When(x => x.ExpectedVersion.HasValue, () =>
        {
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage(ParkingErrors.Pricing.ReasonRequired.Message)
                .MaximumLength(500)
                .WithMessage(ParkingErrors.Pricing.ReasonTooLong.Message);
        });
    }
}
