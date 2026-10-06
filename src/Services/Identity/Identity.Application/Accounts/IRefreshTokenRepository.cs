using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken refreshToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
}
