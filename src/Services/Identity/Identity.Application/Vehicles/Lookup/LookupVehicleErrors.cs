using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public static class LookupVehicleErrors
{
    public static readonly Error EmptyPlate =
        new("identity.plate_empty", "Biển số xe không được để trống hoặc chỉ chứa khoảng trắng.");

    public static readonly Error InvalidPlateFormat =
        new("identity.plate_invalid_format", "Định dạng biển số xe không hợp lệ theo quy chuẩn.");
}
