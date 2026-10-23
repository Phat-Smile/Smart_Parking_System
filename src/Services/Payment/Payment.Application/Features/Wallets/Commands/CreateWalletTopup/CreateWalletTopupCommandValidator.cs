using FluentValidation;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Options;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;

public class CreateWalletTopupCommandValidator : AbstractValidator<CreateWalletTopupCommand>
{
    public CreateWalletTopupCommandValidator(IOptions<WalletOptions> walletOptions)
    {
        var options = walletOptions.Value;

        // Ràng buộc Idempotency-Key header: bắt buộc từ 8 đến 64 ký tự
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key header is required (8-64 characters).")
            .Length(8, 64)
            .WithMessage("Idempotency-Key header is required (8-64 characters).");

        // Ràng buộc số tiền: phải là số nguyên VND, nằm trong khoảng cấu hình [MinTopUp, MaxTopUp]
        RuleFor(x => x.Amount)
            .Must(amount => amount % 1m == 0m)
            .WithMessage("Top-up amount must be a whole number of VND within the allowed range.")
            .InclusiveBetween(options.MinTopUpVnd, options.MaxTopUpVnd)
            .WithMessage("Top-up amount must be a whole number of VND within the allowed range.");
    }
}
