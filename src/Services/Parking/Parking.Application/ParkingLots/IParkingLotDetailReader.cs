using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotDetailReader
{
    Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken);
}

public sealed record ParkingLotDetail(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    WeeklyOpeningHours? OpeningHours = null,
    CustomerLotFeatures? Features = null,
    int? FloorCount = null,
    int? CarCapacity = null,
    int? MotorbikeCapacity = null,
    ParkingLotPublicInfo? PublicInfo = null);
