using System;
using FluentValidation.TestHelper;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ConfigureReservationAvailabilityPolicyCommandValidatorTests
{
    private readonly ConfigureReservationAvailabilityPolicyCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenLotIdIsEmpty_ShouldHaveValidationError()
    {
        var command = new ConfigureReservationAvailabilityPolicyCommand(Guid.Empty, true, Guid.NewGuid());
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LotId)
            .WithErrorCode(ParkingErrors.ReservationAvailabilityPolicy.LotIdEmpty.Code);
    }

    [Fact]
    public void Validate_WhenReservationEnabledIsNull_ShouldHaveValidationError()
    {
        var command = new ConfigureReservationAvailabilityPolicyCommand(Guid.NewGuid(), null, Guid.NewGuid());
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ReservationEnabled)
            .WithErrorCode(ParkingErrors.ReservationAvailabilityPolicy.ReservationEnabledRequired.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WhenValid_ShouldNotHaveValidationError(bool enabled)
    {
        var command = new ConfigureReservationAvailabilityPolicyCommand(Guid.NewGuid(), enabled, Guid.NewGuid());
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
