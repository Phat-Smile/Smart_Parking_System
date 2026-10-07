using ParkingSystem.Identity.Application.Common.Security;

namespace ParkingSystem.Identity.Infrastructure.Security;

// Context NFR-07: passwords are hashed with bcrypt.
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("DummyTimingMitigationPassword", WorkFactor);

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);

    public void VerifyDummy(string password) => BCrypt.Net.BCrypt.Verify(password, DummyHash);
}
