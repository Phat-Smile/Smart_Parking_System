namespace ParkingSystem.Parking.Persistence.ParkingLayout;

// Configuration section "Availability".
public sealed class AvailabilityOptions
{
    public const string SectionName = "Availability";
    public const string DatabaseProvider = "Database";
    public const string StubProvider = "Stub";
    public const int DefaultStubAvailableSlotCount = 10;

    // Database counts real slots. Stub is for development until lot owners can declare slots.
    public string Provider { get; init; } = DatabaseProvider;

    public int StubAvailableSlotCount { get; init; } = DefaultStubAvailableSlotCount;
}
