using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Queries.GetDepositPolicy;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class DepositPolicyCommandHandlerTests
{
    private readonly FakeReservationDepositPolicyRepository _repository = new();
    private readonly FakeLotOwnershipService _lotOwnershipService = new();
    private readonly ConfigureDepositPolicyCommandHandler _configureHandler;
    private readonly GetDepositPolicyQueryHandler _getHandler;

    public DepositPolicyCommandHandlerTests()
    {
        _configureHandler = new ConfigureDepositPolicyCommandHandler(_repository, _lotOwnershipService);
        _getHandler = new GetDepositPolicyQueryHandler(_repository, _lotOwnershipService);
    }

    [Fact]
    public async Task Configure_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var command = new ConfigureDepositPolicyCommand(lotId, 20000m, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.DepositPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var command = new ConfigureDepositPolicyCommand(lotId, 20000m, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.DepositPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenValidAndNoExistingPolicy_ShouldCreatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureDepositPolicyCommand(lotId, 25000m, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.Equal(25000m, result.Value.HoldDepositVnd);
        Assert.True(result.Value.IsConfigured);
        Assert.Single(_repository.Policies);
        Assert.Equal(25000m, _repository.Policies[0].HoldDepositVnd);
    }

    [Fact]
    public async Task Configure_WhenValidAndExistingPolicyExists_ShouldUpdatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var existing = ReservationDepositPolicy.Create(lotId, 10000m);
        _repository.Policies.Add(existing);

        var command = new ConfigureDepositPolicyCommand(lotId, 30000m, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(30000m, result.Value!.HoldDepositVnd);
        Assert.Single(_repository.Policies);
        Assert.Equal(30000m, _repository.Policies[0].HoldDepositVnd);
    }

    [Fact]
    public async Task Configure_WhenHoldDepositIsZero_ShouldStoreZeroSuccessfully()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureDepositPolicyCommand(lotId, 0m, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.HoldDepositVnd);
        Assert.Single(_repository.Policies);
        Assert.Equal(0m, _repository.Policies[0].HoldDepositVnd);
    }

    [Fact]
    public async Task Get_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var query = new GetDepositPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.DepositPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var query = new GetDepositPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.DepositPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenUnconfigured_ShouldReturnIsConfiguredFalseAndNullDeposit()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var query = new GetDepositPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.False(result.Value.IsConfigured);
        Assert.Null(result.Value.HoldDepositVnd);
    }

    [Fact]
    public async Task Get_WhenConfigured_ShouldReturnHoldDepositAmount()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var policy = ReservationDepositPolicy.Create(lotId, 15000m);
        _repository.Policies.Add(policy);

        var query = new GetDepositPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.True(result.Value.IsConfigured);
        Assert.Equal(15000m, result.Value.HoldDepositVnd);
    }

    private sealed class FakeLotOwnershipService : ILotOwnershipService
    {
        private readonly Dictionary<Guid, (bool Exists, bool IsOwner)> _lots = new();

        public void SetupLot(Guid lotId, bool exists, bool isOwner)
        {
            _lots[lotId] = (exists, isOwner);
        }

        public Task<bool> LotExistsAsync(Guid lotId) =>
            Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.Exists);

        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) =>
            Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.IsOwner);
    }

    private sealed class FakeReservationDepositPolicyRepository : IReservationDepositPolicyRepository
    {
        public List<ReservationDepositPolicy> Policies { get; } = new();

        public Task<ReservationDepositPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            var policy = Policies.Find(p => p.LotId == lotId);
            return Task.FromResult(policy);
        }

        public Task AddAsync(ReservationDepositPolicy policy, CancellationToken cancellationToken = default)
        {
            Policies.Add(policy);
            return Task.CompletedTask;
        }

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
