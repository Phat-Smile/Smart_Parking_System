namespace ParkingSystem.Identity.Application.Common.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    void VerifyDummy(string password);
}
