using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.GetCurrentAccount;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// /me đọc Account + Profile thật; họ tên có thể chưa có vì đăng ký không bắt buộc nhập.
public sealed class GetCurrentAccountIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_the_account_with_its_profile_name_and_role()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedAsync(provider, "me-profile@example.com", "Nguyễn Văn A", AccountRole.Staff, cancellationToken);

        var result = await HandleAsync(provider, accountId, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("me-profile@example.com", result.Value!.Email);
        Assert.Equal("Nguyễn Văn A", result.Value.FullName);
        Assert.Equal(nameof(AccountRole.Staff), result.Value.Role);
        Assert.Equal(nameof(AccountStatus.EmailVerified), result.Value.Status);
        Assert.True(result.Value.HasPassword);
        Assert.False(result.Value.GoogleLinked);
    }

    [Fact]
    public async Task Account_without_profile_has_no_name_and_unknown_account_is_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedAsync(provider, "me-noname@example.com", fullName: null, AccountRole.Customer, cancellationToken);

        var withoutName = await HandleAsync(provider, accountId, cancellationToken);
        var unknown = await HandleAsync(provider, Guid.NewGuid(), cancellationToken);

        Assert.Null(withoutName.Value!.FullName);
        Assert.Equal(UnlockAccountErrors.AccountNotFound.Code, unknown.Error?.Code);
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> SeedAsync(ServiceProvider provider, string email, string? fullName, AccountRole role, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(role);
        accounts.Add(account);
        if (fullName is not null)
            scope.ServiceProvider.GetRequiredService<IProfileRepository>().Add(Profile.Create(account.Id, fullName, Now));
        await accounts.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    private static async Task<ParkingSystem.BuildingBlocks.Results.Result<CurrentAccount>> HandleAsync(
        ServiceProvider provider, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var handler = new GetCurrentAccountQueryHandler(
            scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
            scope.ServiceProvider.GetRequiredService<IProfileRepository>());
        return await handler.Handle(new GetCurrentAccountQuery(accountId), cancellationToken);
    }
}
