using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Persistence.Outbox;
using ParkingSystem.Payment.Persistence.Inbox;

namespace ParkingSystem.Payment.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "payment";

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();
    public DbSet<VnpayTransaction> VnpayTransactions => Set<VnpayTransaction>();
    public DbSet<ParkingFeePayment> ParkingFeePayments => Set<ParkingFeePayment>();
    public DbSet<PaymentIdempotencyRecord> PaymentIdempotencyRecords => Set<PaymentIdempotencyRecord>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
