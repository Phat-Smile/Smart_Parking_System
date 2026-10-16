using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ParkingSystem.Payment.Application.Behaviors;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    public record DummyRequest : IRequest<string>;

    [Fact]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<DummyRequest>>();
        var behavior = new ValidationBehavior<DummyRequest, string>(validators);
        var request = new DummyRequest();

        // Act
        var result = await behavior.Handle(
            request,
            (cancellationToken) => Task.FromResult("Success"),
            CancellationToken.None);

        // Assert
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldThrowValidationException()
    {
        // Arrange
        var validator = new InlineValidator<DummyRequest>();
        validator.RuleFor(x => x).Custom((_, context) =>
        {
            context.AddFailure(new ValidationFailure("Property", "Validation failed"));
        });

        var behavior = new ValidationBehavior<DummyRequest, string>(new[] { validator });
        var request = new DummyRequest();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(
                request,
                (cancellationToken) => Task.FromResult("Success"),
                CancellationToken.None));
    }
}
