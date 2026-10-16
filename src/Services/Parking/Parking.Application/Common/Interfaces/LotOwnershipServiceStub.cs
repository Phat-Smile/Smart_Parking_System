using System;
using System.Threading.Tasks;

namespace ParkingSystem.Parking.Application.Common.Interfaces;

public class LotOwnershipServiceStub : ILotOwnershipService
{
    public Task<bool> LotExistsAsync(Guid lotId)
    {
        // Luôn giả vờ là bãi xe có tồn tại để test qua ải
        return Task.FromResult(true);
    }

    public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId)
    {
        // Giả vờ user nào gọi API cũng chính là chủ bãi xe
        return Task.FromResult(true);
    }
}
