namespace ParkingSystem.Parking.Application.Common.Persistence;

// Groups several writes of the Parking database into one transaction, for steps that cannot be a single SaveChanges
// (for example an atomic SQL update followed by an entity insert).
public interface IParkingUnitOfWork
{
    Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

// Disposing without CommitAsync rolls every write back.
public interface IParkingTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
