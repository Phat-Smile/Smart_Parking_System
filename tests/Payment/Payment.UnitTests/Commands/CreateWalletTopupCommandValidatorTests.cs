using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public class CreateWalletTopupCommandValidatorTests
{
    private readonly CreateWalletTopupCommandValidator _validator;

    public CreateWalletTopupCommandValidatorTests()
    {
        var options = Options.Create(new WalletOptions
        {
            MinTopUpVnd = 10_000m,
            MaxTopUpVnd = 5_000_000m,
            MaxBalanceVnd = 10_000_000m
        });

        _validator = new CreateWalletTopupCommandValidator(options);
    }

    [Fact]
    public void Validate_WhenAmountBelowMinimum_HasValidationError()
    {
        // TC-TOP-03: Amount 9,999 -> 400 Bad Request
        var command = new CreateWalletTopupCommand(Guid.NewGuid(), 9_999m, "valid-key-12345", "127.0.0.1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_WhenAmountAboveMaximum_HasValidationError()
    {
        // TC-TOP-04: Amount 5,000,001 -> 400 Bad Request
        var command = new CreateWalletTopupCommand(Guid.NewGuid(), 5_000_001m, "valid-key-12345", "127.0.0.1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(5_000_000)]
    public void Validate_WhenAmountAtBoundaries_IsValid(decimal amount)
    {
        // TC-TOP-05: Boundary amounts 10,000 and 5,000,000 are valid
        var command = new CreateWalletTopupCommand(Guid.NewGuid(), amount, "valid-key-12345", "127.0.0.1");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData(10_000.5)]
    [InlineData(0)]
    [InlineData(-1000)]
    public void Validate_WhenAmountNotWholeNumberOrNonPositive_HasValidationError(decimal amount)
    {
        // TC-TOP-06: Amount not a whole number or non-positive
        var command = new CreateWalletTopupCommand(Guid.NewGuid(), amount, "valid-key-12345", "127.0.0.1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")] // 7 chars (too short)
    public void Validate_WhenIdempotencyKeyTooShortOrEmpty_HasValidationError(string key)
    {
        // TC-TOP-10: Idempotency-Key < 8 chars
        var command = new CreateWalletTopupCommand(Guid.NewGuid(), 100_000m, key, "127.0.0.1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
