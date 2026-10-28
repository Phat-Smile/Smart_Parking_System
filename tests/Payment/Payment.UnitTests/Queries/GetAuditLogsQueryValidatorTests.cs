using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public sealed class GetAuditLogsQueryValidatorTests
{
    private readonly GetAuditLogsQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageLessThan1_HasValidationError(int page)
    {
        var query = new GetAuditLogsQuery(Page: page);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AuditLogErrors.InvalidPage.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(100)]
    public void Validate_WhenPageSizeOutOfRange_HasValidationError(int pageSize)
    {
        var query = new GetAuditLogsQuery(PageSize: pageSize);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AuditLogErrors.InvalidPageSize.Message);
    }

    [Fact]
    public void Validate_WhenActionTypeIsInvalid_HasValidationError()
    {
        var query = new GetAuditLogsQuery(ActionType: "InvalidActionXYZ");
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AuditLogErrors.InvalidActionType.Message);
    }

    [Fact]
    public void Validate_WhenFromUtcIsAfterToUtc_HasValidationError()
    {
        var from = DateTime.UtcNow;
        var to = from.AddHours(-1);
        var query = new GetAuditLogsQuery(FromUtc: from, ToUtc: to);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AuditLogErrors.InvalidDateRange.Message);
    }

    [Fact]
    public void Validate_WhenFromUtcEqualsToUtc_HasValidationError()
    {
        var time = DateTime.UtcNow;
        var query = new GetAuditLogsQuery(FromUtc: time, ToUtc: time);
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AuditLogErrors.InvalidDateRange.Message);
    }

    [Fact]
    public void Validate_WhenValidParameters_PassesValidation()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var query = new GetAuditLogsQuery(
            Page: 1,
            PageSize: 20,
            ActionType: "CashPaymentConfirmed",
            ParkingLotId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            FromUtc: from,
            ToUtc: to);

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenDefaultParameters_PassesValidation()
    {
        var query = new GetAuditLogsQuery();
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
