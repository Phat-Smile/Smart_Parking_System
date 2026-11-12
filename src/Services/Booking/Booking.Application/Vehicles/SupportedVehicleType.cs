namespace ParkingSystem.Booking.Application.Vehicles;

/// <summary>
/// Domain-level vehicle type known to Booking application.
/// Isolates the Application layer from gRPC Protobuf generated contracts.
/// </summary>
public enum SupportedVehicleType
{
    Unspecified = 0,
    Car = 1,
    Motorbike = 2
}

