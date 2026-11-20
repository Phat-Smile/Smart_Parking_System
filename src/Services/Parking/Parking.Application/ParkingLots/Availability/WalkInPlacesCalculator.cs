namespace ParkingSystem.Parking.Application.ParkingLots.Availability;

/// <summary>
/// Pure calculation helper demonstrating the formula and display logic for walk-in protection (BR-02.d).
/// Formula: Walk-in places = max(0, Available slots − held quota).
/// Display:
/// - If Available = 0: Status "full", Message "Đã đầy"
/// - If Available > 0 and Walk-in places = 0: Status "walkInFull", Message "Hết chỗ cho xe không đặt trước"
/// - If Walk-in places > 0: Status "available", Message "Còn chỗ"
/// (This logic lives in Parking service; Booking owns the held quota).
/// </summary>
public static class WalkInPlacesCalculator
{
    public const string FullMessage = "Đã đầy";
    public const string NoPlacesForUnreservedMessage = "Hết chỗ cho xe không đặt trước";
    public const string AvailableMessage = "Còn chỗ";

    public const string StatusFull = "full";
    public const string StatusWalkInFull = "walkInFull";
    public const string StatusAvailable = "available";

    public static (int WalkInPlaces, string Status, string DisplayMessage) Calculate(int availableSlots, int heldQuota)
    {
        if (availableSlots <= 0)
        {
            return (0, StatusFull, FullMessage);
        }

        var walkInPlaces = Math.Max(0, availableSlots - Math.Max(0, heldQuota));
        if (walkInPlaces == 0)
        {
            return (0, StatusWalkInFull, NoPlacesForUnreservedMessage);
        }

        return (walkInPlaces, StatusAvailable, AvailableMessage);
    }
}
