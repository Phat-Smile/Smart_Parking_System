namespace ParkingSystem.Parking.Domain.ParkingLots;

public sealed class ParkingLot
{
    private ParkingLot()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public ParkingLotType Type { get; private set; }
    public ParkingLotApprovalStatus ApprovalStatus { get; private set; }
}
