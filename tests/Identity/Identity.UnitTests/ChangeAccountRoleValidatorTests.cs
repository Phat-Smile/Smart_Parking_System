using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class ChangeAccountRoleValidatorTests
{
    private readonly ChangeAccountRoleCommandValidator _validator = new();

    [Theory]
    [InlineData("Staff", true)]
    [InlineData("parkinglotowner", true)] // không phân biệt hoa thường
    [InlineData("SystemAdmin", true)]
    [InlineData("", false)]
    [InlineData("Root", false)]
    [InlineData("2", false)]              // số của enum không được nhận thay cho tên
    public void Accepts_only_role_names(string role, bool expectedValid)
    {
        var result = _validator.Validate(new ChangeAccountRoleCommand(Guid.NewGuid(), Guid.NewGuid(), role));
        Assert.Equal(expectedValid, result.IsValid);
    }
}
