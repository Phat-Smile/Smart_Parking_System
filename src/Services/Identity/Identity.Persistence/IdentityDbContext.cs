using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Domain.Mfa;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Domain.Vehicles;
using ParkingSystem.Identity.Persistence.Inbox;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Identity.Persistence.Profiles;

namespace ParkingSystem.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "identity";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<CustomerMedia> CustomerMedia => Set<CustomerMedia>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<OwnerApplicationDocument> OwnerApplicationDocuments => Set<OwnerApplicationDocument>();

    public DbSet<AccountMfa> AccountMfas => Set<AccountMfa>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
