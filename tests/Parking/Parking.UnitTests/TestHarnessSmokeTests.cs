using System.Reflection;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void Application_and_domain_assemblies_are_available_to_the_unit_test_host()
    {
        Assert.Equal("Parking.Application", Assembly.Load("Parking.Application").GetName().Name);
        Assert.Equal("Parking.Domain", Assembly.Load("Parking.Domain").GetName().Name);
    }
}
