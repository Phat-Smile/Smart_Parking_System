namespace ParkingSystem.Identity.Application.Common.Security;

public interface IOtpGenerator
{
    string GenerateNumericOtp(int length = 6);
}
