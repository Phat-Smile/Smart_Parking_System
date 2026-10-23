using System;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class GetPricingPolicyQueryTests
{
    private readonly FakePricingPolicyRepository _pricingRepository = new();
    private readonly FakeLotOwnershipService _lotOwnershipService = new();
    private readonly GetPricingPolicyQueryHandler _handler;

    public GetPricingPolicyQueryTests()
    {
        _handler = new GetPricingPolicyQueryHandler(
            _pricingRepository,
            _lotOwnershipService);
    }

    [Fact]
    public async Task Handle_WhenLotIdEmpty_ReturnsLotIdEmptyError()
    {
        var query = new GetPricingPolicyQuery(Guid.Empty, "CAR");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.LotIdEmpty.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WhenVehicleTypeEmpty_ReturnsVehicleTypeEmptyError(string? vehicleType)
    {
        var query = new GetPricingPolicyQuery(Guid.NewGuid(), vehicleType!);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.VehicleTypeEmpty.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("TRUCK")]
    [InlineData("BICYCLE")]
    [InlineData("PLANE")]
    public async Task Handle_WhenVehicleTypeInvalid_ReturnsVehicleTypeInvalidError(string vehicleType)
    {
        var query = new GetPricingPolicyQuery(Guid.NewGuid(), vehicleType);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.VehicleTypeInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenLotNotFound_ReturnsLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var query = new GetPricingPolicyQuery(lotId, "CAR");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenPricingNotFound_ReturnsPolicyNotFoundError()
    {
        var lotId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var query = new GetPricingPolicyQuery(lotId, "CAR");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.PolicyNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenPricingExists_ReturnsCorrectPricingPolicyDto()
    {
        var lotId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var policy = PricingPolicy.Create(
            lotId,
            "CAR",
            version: 2,
            billingMode: "Block",
            blockMinutes: 30,
            price: 15000m,
            overnightFee: 60000m);

        await _pricingRepository.AddAsync(policy, CancellationToken.None);

        var query = new GetPricingPolicyQuery(lotId, "car"); // lowercase to test normalization
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(lotId, dto.LotId);
        Assert.Equal("CAR", dto.VehicleType);
        Assert.Equal(2, dto.Version);
        Assert.Equal("Block", dto.BillingMode);
        Assert.Equal(30, dto.BlockMinutes);
        Assert.Equal(15000m, dto.Price);
        Assert.Equal(60000m, dto.OvernightFee);
    }

    private sealed class FakeLotOwnershipService : ILotOwnershipService
    {
        private readonly System.Collections.Generic.Dictionary<Guid, (bool Exists, bool IsOwner)> _lots = new();

        public void SetupLot(Guid lotId, bool exists, bool isOwner)
        {
            _lots[lotId] = (exists, isOwner);
        }

        public Task<bool> LotExistsAsync(Guid lotId)
        {
            return Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.Exists);
        }

        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId)
        {
            return Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.IsOwner);
        }
    }

    private sealed class FakePricingPolicyRepository : IPricingPolicyRepository
    {
        public System.Collections.Generic.List<PricingPolicy> Policies { get; } = new();

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken)
        {
            var policy = Policies.FindLast(p => p.LotId == lotId && p.VehicleType == vehicleType);
            return Task.FromResult(policy);
        }

        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken)
        {
            Policies.Add(policy);
            return Task.CompletedTask;
        }

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
