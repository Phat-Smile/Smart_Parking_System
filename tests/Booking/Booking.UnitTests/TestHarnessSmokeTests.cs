using System.Reflection;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void Application_and_domain_assemblies_are_available_to_the_unit_test_host()
    {
        Assert.Equal("Booking.Application", Assembly.Load("Booking.Application").GetName().Name);
        Assert.Equal("Booking.Domain", Assembly.Load("Booking.Domain").GetName().Name);
    }
}
