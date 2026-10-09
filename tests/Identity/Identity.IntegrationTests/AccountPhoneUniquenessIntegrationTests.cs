using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class AccountPhoneUniquenessIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Second_account_with_the_same_phone_reports_a_phone_conflict_instead_of_throwing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
            await migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);

        var createdAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            repository.Add(Account.Register(Guid.NewGuid(), "first-phone@example.com", "hash-1", "0912345678", createdAt));
            Assert.Equal(SaveAccountResult.Success, await repository.SaveNewAccountAsync(cancellationToken));
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            // Same number written as +84 normalizes to the same stored value.
            repository.Add(Account.Register(Guid.NewGuid(), "second-phone@example.com", "hash-2", "+84912345678", createdAt));
            Assert.Equal(SaveAccountResult.DuplicatePhoneConflict, await repository.SaveNewAccountAsync(cancellationToken));
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var emails = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts
                .AsNoTracking().Select(account => account.Email).ToListAsync(cancellationToken);
            Assert.Equal(["first-phone@example.com"], emails);
        }
    }
}
