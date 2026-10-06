using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

// Context NFR-07: passwords are hashed with bcrypt.
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
