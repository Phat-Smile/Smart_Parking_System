using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class AccountRepositoryIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Accounts_persist_and_the_unique_email_index_rejects_a_second_registration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
            await migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);

        var createdAt = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var first = Account.Register(Guid.NewGuid(), "user@example.com", "hash-1", null, createdAt);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            Assert.False(await repository.EmailExistsAsync("user@example.com", cancellationToken));
            repository.Add(first);
            Assert.True(await repository.TrySaveChangesAsync(cancellationToken));
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            Assert.True(await repository.EmailExistsAsync("user@example.com", cancellationToken));

            // Bypasses the pre-check, as a concurrent request would, and must lose on the unique index.
            repository.Add(Account.Register(Guid.NewGuid(), "User@Example.com", "hash-2", null, createdAt));
            Assert.False(await repository.TrySaveChangesAsync(cancellationToken));
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts
                .AsNoTracking()
                .SingleAsync(cancellationToken);
            Assert.Equal(first.Id, stored.Id);
            Assert.Equal("hash-1", stored.PasswordHash);
            Assert.Equal(AccountStatus.PendingEmailVerification, stored.Status);
            Assert.Null(stored.PhoneNumber);
        }
    }
}
