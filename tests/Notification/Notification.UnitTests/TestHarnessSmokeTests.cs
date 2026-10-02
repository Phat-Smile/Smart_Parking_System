using System.Reflection;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void Application_and_domain_assemblies_are_available_to_the_unit_test_host()
    {
        Assert.Equal("Notification.Application", Assembly.Load("Notification.Application").GetName().Name);
        Assert.Equal("Notification.Domain", Assembly.Load("Notification.Domain").GetName().Name);
    }
}
