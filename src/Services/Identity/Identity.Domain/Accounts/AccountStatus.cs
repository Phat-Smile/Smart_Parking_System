namespace ParkingSystem.Identity.Domain.Accounts;

public enum AccountStatus
{
    PendingEmailVerification,
    EmailVerified,
    Suspended,
    Disabled
}
