namespace ParkingSystem.Identity.Domain.EmailVerifications;

// One OTP mechanism (NFR-13) serves several flows; each flow has its own codes and its own hourly quota.
public enum OtpPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}
