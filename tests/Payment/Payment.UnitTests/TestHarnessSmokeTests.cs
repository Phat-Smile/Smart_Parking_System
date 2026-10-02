using System.Reflection;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void Application_and_domain_assemblies_are_available_to_the_unit_test_host()
    {
        Assert.Equal("Payment.Application", Assembly.Load("Payment.Application").GetName().Name);
        Assert.Equal("Payment.Domain", Assembly.Load("Payment.Domain").GetName().Name);
    }
}
