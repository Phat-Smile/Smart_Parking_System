using Microsoft.EntityFrameworkCore.Storage;
using ParkingSystem.Parking.Application.Common.Persistence;

namespace ParkingSystem.Parking.Persistence;

internal sealed class ParkingUnitOfWork(ParkingDbContext dbContext) : IParkingUnitOfWork
{
    public async Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new ParkingTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class ParkingTransaction(IDbContextTransaction transaction) : IParkingTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        // EF rolls an uncommitted transaction back when it is disposed.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
