namespace ParkingSystem.Parking.Domain.ParkingLayout;

// A floor of a multi-storey or basement lot, or the single ground area of an outdoor lot (context section 5).
public sealed class Floor
{
    public const int NameMaxLength = 100;

    private Floor()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public string Name { get; private set; } = null!;

    public static Floor Create(Guid id, Guid lotId, string name) =>
        new() { Id = id, LotId = lotId, Name = name.Trim() };
    public bool Rename(string name)
    {
        var normalized = name.Trim();
        if (Name == normalized) return false;
        Name = normalized;
        return true;
    }
}
