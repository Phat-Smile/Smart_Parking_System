namespace ParkingSystem.Identity.Application.Common.Security;

public interface IRefreshTokenGenerator
{
    (string RawToken, string TokenHash) GenerateToken();

    string HashToken(string rawToken);
}
