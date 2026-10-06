using System.Collections.Concurrent;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Tests.Booking.UnitTests.Fakes;

public sealed class InMemoryReservationCapacityCoordinator : IReservationCapacityCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<TResult> ExecuteUnderCapacityLockAsync<TResult>(
        Guid lotId,
        VehicleGroup vehicleGroup,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var key = $"{lotId}:{vehicleGroup}";
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task RecordCapacityUpdatedAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
