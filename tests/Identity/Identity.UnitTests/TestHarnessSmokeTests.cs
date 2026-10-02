using System.Reflection;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void Application_and_domain_assemblies_are_available_to_the_unit_test_host()
    {
        Assert.Equal("Identity.Application", Assembly.Load("Identity.Application").GetName().Name);
        Assert.Equal("Identity.Domain", Assembly.Load("Identity.Domain").GetName().Name);
    }
}
