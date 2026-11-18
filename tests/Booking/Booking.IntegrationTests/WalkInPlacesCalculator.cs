namespace ParkingSystem.Tests.Booking.IntegrationTests;

/// <summary>
/// Pure calculation helper demonstrating the formula and display logic for walk-in protection (BR-02.d).
/// Formula: Walk-in places = max(0, Available slots − held quota).
/// Display:
/// - If Available = 0: "Đã đầy"
/// - If Available > 0 and Walk-in places = 0: "Hết chỗ cho xe không đặt trước"
/// - If Walk-in places > 0: "Còn chỗ"
/// (This logic lives in Parking service; Booking owns the held quota).
/// </summary>
public static class WalkInPlacesCalculator
{
    public const string FullMessage = "Đã đầy";
    public const string NoPlacesForUnreservedMessage = "Hết chỗ cho xe không đặt trước";
    public const string AvailableMessage = "Còn chỗ";

    public static (int WalkInPlaces, string DisplayMessage) Calculate(int availableSlots, int heldQuota)
    {
        if (availableSlots <= 0)
        {
            return (0, FullMessage);
        }

        var walkInPlaces = Math.Max(0, availableSlots - heldQuota);
        if (walkInPlaces == 0)
        {
            return (0, NoPlacesForUnreservedMessage);
        }

        return (walkInPlaces, AvailableMessage);
    }
}

