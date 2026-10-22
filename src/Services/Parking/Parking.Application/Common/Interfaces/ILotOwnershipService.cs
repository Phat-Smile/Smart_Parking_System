using System;
using System.Threading.Tasks;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

/// <summary>
/// INTERFACE TẠM (STUB): Chờ Khoa (BE-2) cung cấp API thật thì sẽ thay thế Adapter.
/// </summary>
public interface ILotOwnershipService
{
    Task<bool> LotExistsAsync(Guid lotId);
    Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId);
}
