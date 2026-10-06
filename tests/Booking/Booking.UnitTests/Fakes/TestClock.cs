using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Tests.Booking.UnitTests.Fakes;

public sealed class TestClock(DateTimeOffset initialUtcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = initialUtcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
