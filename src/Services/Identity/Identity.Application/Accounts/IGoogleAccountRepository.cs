using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

// Lookup by the Google "sub" claim (stable even if the Google email changes); same DbContext as IAccountRepository.
public interface IGoogleAccountRepository
{
    Task<Account?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken);

    Task<Account?> FindByGoogleSubjectForUpdateAsync(string googleSubject, CancellationToken cancellationToken);
}
