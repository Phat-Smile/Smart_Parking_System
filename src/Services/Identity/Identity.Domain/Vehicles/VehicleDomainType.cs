namespace ParkingSystem.Identity.Domain.Vehicles;

// Bicycle remains mapped so existing records can be read; new registrations only allow Car and Motorbike.
public enum VehicleDomainType
{
    Car = 1,
    Motorbike = 2,
    Bicycle = 3
}
