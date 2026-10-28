using System.Text.RegularExpressions;
using FluentValidation;
using ParkingSystem.Payment.Application.Common.Constants;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;

public class GetWalletTopupStatusQueryValidator : AbstractValidator<GetWalletTopupStatusQuery>
{
    private static readonly Regex Hex32Regex = new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    public GetWalletTopupStatusQueryValidator()
    {
        // Ma giao dich bat buoc dung 32 ky tu hex
        RuleFor(x => x.TopupId)
            .NotEmpty().WithMessage(PaymentErrors.Topup.InvalidId.Message)
            .Must(id => !string.IsNullOrWhiteSpace(id) && Hex32Regex.IsMatch(id))
            .WithMessage(PaymentErrors.Topup.InvalidId.Message);
    }
}
