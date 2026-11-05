using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class AdminParkingLotReviewQueryTests
{
    [Fact]
    public async Task Admin_detail_uses_an_unfiltered_reader_for_the_requested_lot()
    {
        var id = Guid.NewGuid();
        var expected = new AdminParkingLotDetail(id, Guid.NewGuid(), "Test", "Address", 10m, 106m,
            "Outdoor", "Pending", null, null);
        var reader = new DetailReader(expected);
        var result = await new GetAdminParkingLotDetailQueryHandler(reader).Handle(
            new GetAdminParkingLotDetailQuery(id), TestContext.Current.CancellationToken);
        Assert.Same(expected, result);
        Assert.Equal(id, reader.LastId);
    }

    [Fact]
    public async Task Admin_layout_reads_even_when_the_requester_is_not_the_owner()
    {
        var lotId = Guid.NewGuid();
        var repository = new LayoutRepository(lotId);
        var result = await new GetAdminParkingLayoutQueryHandler(repository).Handle(
            new GetAdminParkingLayoutQuery(lotId), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(0, repository.OwnerCheckCount);
    }

    [Fact]
    public async Task Admin_layout_returns_not_found_for_an_unknown_lot()
    {
        var repository = new LayoutRepository(Guid.NewGuid());
        var result = await new GetAdminParkingLayoutQueryHandler(repository).Handle(
            new GetAdminParkingLayoutQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(ParkingLayoutErrors.LotNotFound.Code, result.Error!.Code);
        Assert.Equal(0, repository.ReadCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("truck")]
    [InlineData("1")]
    public async Task Admin_pricing_rejects_invalid_vehicle_type(string vehicleType)
    {
        var result = await new GetAdminPricingPolicyQueryHandler(new PricingRepository(), new LotOwnershipService())
            .Handle(new GetAdminPricingPolicyQuery(Guid.NewGuid(), vehicleType), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Contains("validation", result.Error!.Code);
    }

    [Fact]
    public async Task Admin_pricing_reads_pending_lot_without_owner_or_approval_checks()
    {
        var lot = Guid.NewGuid();
        var pricing = new PricingRepository
        {
            Policy = PricingPolicy.Create(lot, "CAR", 2, "Block", 30, 15000m, 60000m)
        };
        var ownership = new LotOwnershipService { Exists = true };
        var result = await new GetAdminPricingPolicyQueryHandler(pricing, ownership)
            .Handle(new GetAdminPricingPolicyQuery(lot, " car "), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(15000m, result.Value!.Price);
        Assert.Equal("CAR", pricing.LastVehicleType);
        Assert.Equal(0, ownership.OwnerChecks);
        Assert.Equal(0, ownership.ApprovalChecks);
    }

    [Fact]
    public async Task Admin_pricing_distinguishes_unknown_lot_and_unconfigured_vehicle_price()
    {
        var lot = Guid.NewGuid();
        var pricing = new PricingRepository();
        var ownership = new LotOwnershipService { Exists = false };
        var handler = new GetAdminPricingPolicyQueryHandler(pricing, ownership);
        var missingLot = await handler.Handle(new GetAdminPricingPolicyQuery(lot, "CAR"), TestContext.Current.CancellationToken);
        Assert.Equal(ParkingErrors.Pricing.LotNotFound.Code, missingLot.Error!.Code);
        ownership.Exists = true;
        var missingPrice = await handler.Handle(new GetAdminPricingPolicyQuery(lot, "CAR"), TestContext.Current.CancellationToken);
        Assert.Equal(ParkingErrors.Pricing.PolicyNotFound.Code, missingPrice.Error!.Code);
    }

    private sealed class DetailReader(AdminParkingLotDetail? item) : IAdminParkingLotDetailReader
    {
        public Guid LastId { get; private set; }
        public Task<AdminParkingLotDetail?> GetByIdAsync(Guid parkingLotId, CancellationToken ct)
        {
            LastId = parkingLotId;
            return Task.FromResult(item);
        }
    }

    private sealed class LayoutRepository(Guid knownLot) : IManageParkingLayoutRepository
    {
        public int ReadCount { get; private set; }
        public int OwnerCheckCount { get; private set; }
        public Task<bool> LotExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(id == knownLot);
        public Task<bool> IsApprovedAsync(Guid id, CancellationToken ct) => Task.FromResult(id == knownLot);
        public Task<bool> IsOwnerAsync(Guid id, Guid actor, CancellationToken ct)
        {
            OwnerCheckCount++;
            return Task.FromResult(false);
        }
        public Task<ParkingLotLayout> ReadAsync(Guid id, CancellationToken ct)
        {
            ReadCount++;
            return Task.FromResult(new ParkingLotLayout(id, 0, []));
        }
        public Task<Error?> SaveAsync(Guid id, Guid actor, int version, IReadOnlyList<LayoutFloorInput> floors,
            CancellationToken ct) => Task.FromResult<Error?>(null);
    }

    private sealed class LotOwnershipService : ILotOwnershipService
    {
        public bool Exists { get; set; }
        public int OwnerChecks { get; private set; }
        public int ApprovalChecks { get; private set; }
        public Task<bool> LotExistsAsync(Guid id) => Task.FromResult(Exists);
        public Task<bool> IsLotOwnerAsync(Guid id, Guid caller)
        {
            OwnerChecks++;
            return Task.FromResult(false);
        }
        public Task<bool> IsLotApprovedAsync(Guid id)
        {
            ApprovalChecks++;
            return Task.FromResult(false);
        }
    }

    private sealed class PricingRepository : IPricingPolicyRepository
    {
        public PricingPolicy? Policy { get; set; }
        public string? LastVehicleType { get; private set; }
        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken ct)
        {
            LastVehicleType = vehicleType;
            return Task.FromResult(Policy);
        }
        public Task AddAsync(PricingPolicy policy, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
