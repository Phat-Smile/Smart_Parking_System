using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles.UpsertProfile;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Application.Vehicles.AddVehicle;
using ParkingSystem.Identity.Application.Vehicles.GetVehicles;
using ParkingSystem.Identity.Application.Vehicles.GetVehicle;
using ParkingSystem.Identity.Application.Vehicles.UpdateVehicle;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Vehicles;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// US-PROFILE-001/002 and US-VEH-001 through the real handlers, validators and repositories on PostgreSQL.
public sealed class ProfileAndVehicleIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private async Task<ServiceProvider> BuildAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["VehicleLookup:Provider"] = "Database" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration);
        services.AddApplicationServices();
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<Guid> NewVerifiedAccountAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var id = Guid.NewGuid();
        var account = Account.Register(id, $"{id:N}@example.com", "hash", null, DateTimeOffset.UtcNow);
        account.VerifyEmail();
        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        repository.Add(account);
        Assert.Equal(SaveAccountResult.Success, await repository.SaveNewAccountAsync(TestContext.Current.CancellationToken));
        return id;
    }

    private static async Task<T> SendAsync<T>(ServiceProvider provider, IRequest<T> request)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_vehicle_added_with_a_formatted_plate_is_found_by_the_plate_lookup_used_at_the_gate()
    {
        await using var provider = await BuildAsync();
        var accountId = await NewVerifiedAccountAsync(provider);
        var plate = $"51A-{Random.Shared.Next(100, 999)}.{Random.Shared.Next(10, 99)}";

        var added = await SendAsync(provider, new AddVehicleCommand(accountId, plate.ToLowerInvariant(), "car", "Xe di lam"));
        Assert.True(added.IsSuccess, added.Error?.Message);

        var canonical = plate.Replace("-", "").Replace(".", "");
        Assert.Equal(canonical, added.Value!.LicensePlate);

        var lookup = await SendAsync(provider, new LookupVehicleByPlateQuery(canonical));
        Assert.True(lookup.IsSuccess);
        Assert.True(lookup.Value!.IsRegistered);
        Assert.Equal(accountId, lookup.Value.AccountId);
        Assert.Equal(added.Value.VehicleId, lookup.Value.VehicleId);
        Assert.True(lookup.Value.IsAccountActive);
    }

    [Fact]
    public async Task A_plate_owned_by_another_account_is_rejected_whatever_its_formatting()
    {
        await using var provider = await BuildAsync();
        var first = await NewVerifiedAccountAsync(provider);
        var second = await NewVerifiedAccountAsync(provider);
        var suffix = Random.Shared.Next(10000, 99999);

        Assert.True((await SendAsync(provider, new AddVehicleCommand(first, $"30F{suffix}", "Car", null))).IsSuccess);
        var duplicate = await SendAsync(provider, new AddVehicleCommand(second, $"30f-{suffix / 100}.{suffix % 100:00}", "Motorbike", null));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(AddVehicleErrors.PlateAlreadyRegistered.Code, duplicate.Error!.Code);
    }

    [Fact]
    public async Task The_unique_index_reports_a_lost_race_as_a_conflict_instead_of_throwing()
    {
        await using var provider = await BuildAsync();
        var first = await NewVerifiedAccountAsync(provider);
        var second = await NewVerifiedAccountAsync(provider);
        var plate = $"29A{Random.Shared.Next(10000, 99999)}";

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            repository.Add(Vehicle.Add(Guid.NewGuid(), first, plate, VehicleDomainType.Car, DateTimeOffset.UtcNow));
            Assert.Equal(SaveVehicleResult.Success, await repository.SaveNewVehicleAsync(TestContext.Current.CancellationToken));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            // The existence check passed earlier for this caller; only the database index can still stop it.
            repository.Add(Vehicle.Add(Guid.NewGuid(), second, plate, VehicleDomainType.Car, DateTimeOffset.UtcNow));
            Assert.Equal(SaveVehicleResult.DuplicatePlateConflict, await repository.SaveNewVehicleAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Each_account_sees_only_its_own_vehicles_in_creation_order()
    {
        await using var provider = await BuildAsync();
        var mine = await NewVerifiedAccountAsync(provider);
        var other = await NewVerifiedAccountAsync(provider);
        var one = $"51B{Random.Shared.Next(10000, 99999)}";
        var two = $"51C{Random.Shared.Next(10000, 99999)}";
        await SendAsync(provider, new AddVehicleCommand(mine, one, "Car", null));
        await SendAsync(provider, new AddVehicleCommand(mine, two, "Motorbike", null));
        await SendAsync(provider, new AddVehicleCommand(other, $"51D{Random.Shared.Next(10000, 99999)}", "Car", null));

        var list = await SendAsync(provider, new GetVehiclesQuery(mine));

        Assert.Equal([one, two], list.Value!.Vehicles.Select(v => v.LicensePlate).ToArray());
    }

    [Fact]
    public async Task Customer_vehicle_details_round_trip_and_other_account_is_denied()
    {
        await using var provider = await BuildAsync();
        var ownerId = await NewVerifiedAccountAsync(provider);
        var otherId = await NewVerifiedAccountAsync(provider);
        var plate = $"51E{Random.Shared.Next(10000, 99999)}";
        var added = await SendAsync(provider, new AddVehicleCommand(ownerId, plate, "Car", "Family car"));
        Assert.True(added.IsSuccess);
        var details = new VehicleDetails("Toyota", "Vios", null, 5, "gasoline", "white", 2020, "standard", null);

        var updated = await SendAsync(provider, new UpdateVehicleCommand(ownerId, added.Value!.VehicleId, "Daily car", details));
        var loaded = await SendAsync(provider, new GetVehicleQuery(ownerId, added.Value.VehicleId));
        var denied = await SendAsync(provider, new GetVehicleQuery(otherId, added.Value.VehicleId));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Toyota", loaded.Value!.Details!.Brand);
        Assert.Equal("Daily car", loaded.Value.NickName);
        Assert.False(denied.IsSuccess);
    }

    [Fact]
    public async Task Customer_can_register_multiple_bicycles_without_plates()
    {
        await using var provider = await BuildAsync();
        var accountId = await NewVerifiedAccountAsync(provider);
        var photo1 = new VehicleDetails(null, null, null, null, null, "red", null, null,
            $"/api/identity/accounts/me/media/{Guid.NewGuid()}");
        var photo2 = new VehicleDetails(null, null, null, null, null, "blue", null, null,
            $"/api/identity/accounts/me/media/{Guid.NewGuid()}");

        var first = await SendAsync(provider, new AddVehicleCommand(accountId, null, "Bicycle", "Bike one", photo1));
        var second = await SendAsync(provider, new AddVehicleCommand(accountId, null, "Bicycle", "Bike two", photo2));
        var list = await SendAsync(provider, new GetVehiclesQuery(accountId));

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Null(first.Value!.LicensePlate);
        Assert.Equal(2, list.Value!.Vehicles.Count(x => x.VehicleType == "Bicycle"));
    }

    [Fact]
    public async Task Profile_is_created_then_updated_in_place_and_blank_optional_fields_are_cleared()
    {
        await using var provider = await BuildAsync();
        var accountId = await NewVerifiedAccountAsync(provider);

        var created = await SendAsync(provider, new UpsertProfileCommand(accountId, " Nguyen Van A ", new DateOnly(1995, 5, 15), "012345678901", "B1234567", "https://cdn.example.com/a.png"));
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal("Nguyen Van A", created.Value!.FullName);
        Assert.Null(created.Value.UpdatedAtUtc);

        var updated = await SendAsync(provider, new UpsertProfileCommand(accountId, "Nguyen Van B", null, "   ", null, null));
        Assert.True(updated.IsSuccess);
        Assert.Equal("Nguyen Van B", updated.Value!.FullName);
        Assert.Null(updated.Value.DateOfBirth);
        Assert.Null(updated.Value.CitizenIdNumber);
        Assert.Null(updated.Value.DriverLicenseNumber);
        Assert.NotNull(updated.Value.UpdatedAtUtc);

        await using var scope = provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Profiles
            .AsNoTracking().CountAsync(p => p.AccountId == accountId, TestContext.Current.CancellationToken);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task Profile_for_an_unknown_account_is_reported_not_created()
    {
        await using var provider = await BuildAsync();

        var result = await SendAsync(provider, new UpsertProfileCommand(Guid.NewGuid(), "Nobody", null, null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpsertProfileErrors.AccountNotFound.Code, result.Error!.Code);
    }
}
