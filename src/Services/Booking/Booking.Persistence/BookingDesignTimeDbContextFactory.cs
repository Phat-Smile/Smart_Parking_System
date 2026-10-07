using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParkingSystem.Booking.Persistence;

/// <summary>
/// Design-time factory for EF Core migration generation (dotnet ef migrations add).
/// </summary>
public sealed class BookingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=127.0.0.1;Port=5433;Database=parking_system;Username=parking_dev;Password=parking_dev_local_only",
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(BookingDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.DefaultSchema);
            });

        return new BookingDbContext(optionsBuilder.Options);
    }
}
