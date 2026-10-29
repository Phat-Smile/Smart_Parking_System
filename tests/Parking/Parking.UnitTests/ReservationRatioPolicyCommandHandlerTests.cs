using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Queries.GetReservationRatioPolicy;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ReservationRatioPolicyCommandHandlerTests
{
    private readonly FakeReservationRatioPolicyRepository _repository = new();
    private readonly FakeLotOwnershipService _lotOwnershipService = new();
    private readonly ConfigureReservationRatioPolicyCommandHandler _configureHandler;
    private readonly GetReservationRatioPolicyQueryHandler _getHandler;

    public ReservationRatioPolicyCommandHandlerTests()
    {
        _configureHandler = new ConfigureReservationRatioPolicyCommandHandler(_repository, _lotOwnershipService);
        _getHandler = new GetReservationRatioPolicyQueryHandler(_repository, _lotOwnershipService);
    }

    [Fact]
    public async Task Configure_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 50, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationRatioPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 50, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationRatioPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenValidAndNoExistingPolicy_ShouldCreatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 30, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.Equal(30, result.Value.ReservationRatioPercent);
        Assert.True(result.Value.IsConfigured);
        Assert.Single(_repository.Policies);
        Assert.Equal(30, _repository.Policies[0].ReservationRatioPercent);
    }

    [Fact]
    public async Task Configure_WhenValidAndExistingPolicyExists_ShouldUpdatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var existing = ReservationRatioPolicy.Create(lotId, 20);
        _repository.Policies.Add(existing);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 70, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(70, result.Value!.ReservationRatioPercent);
        Assert.Single(_repository.Policies);
        Assert.Equal(70, _repository.Policies[0].ReservationRatioPercent);
    }

    [Fact]
    public async Task Configure_WhenRatioIsZero_ShouldStoreZeroSuccessfully()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 0, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ReservationRatioPercent);
        Assert.Single(_repository.Policies);
        Assert.Equal(0, _repository.Policies[0].ReservationRatioPercent);
    }

    [Fact]
    public async Task Configure_WhenRatioIs100_ShouldStore100Successfully()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureReservationRatioPolicyCommand(lotId, 100, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.ReservationRatioPercent);
        Assert.Single(_repository.Policies);
        Assert.Equal(100, _repository.Policies[0].ReservationRatioPercent);
    }

    [Fact]
    public async Task Get_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var query = new GetReservationRatioPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationRatioPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var query = new GetReservationRatioPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationRatioPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenUnconfigured_ShouldReturnDefaultRatioFiftyAndIsConfiguredFalseAndNotWriteToDatabase()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var query = new GetReservationRatioPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.False(result.Value.IsConfigured);
        Assert.Equal(ReservationRatioPolicy.DefaultReservationRatioPercent, result.Value.ReservationRatioPercent);
        Assert.Equal(50, result.Value.ReservationRatioPercent);
        Assert.Null(result.Value.UpdatedAtUtc);
        Assert.Empty(_repository.Policies);
    }

    [Fact]
    public async Task Get_WhenConfigured_ShouldReturnConfiguredRatio()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var policy = ReservationRatioPolicy.Create(lotId, 35);
        _repository.Policies.Add(policy);

        var query = new GetReservationRatioPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.True(result.Value.IsConfigured);
        Assert.Equal(35, result.Value.ReservationRatioPercent);
        Assert.NotNull(result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task Get_WhenConfiguredZero_ShouldReturnZeroAndIsConfiguredTrue()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var policy = ReservationRatioPolicy.Create(lotId, 0);
        _repository.Policies.Add(policy);

        var query = new GetReservationRatioPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.True(result.Value.IsConfigured);
        Assert.Equal(0, result.Value.ReservationRatioPercent);
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

        public Task<bool> IsLotApprovedAsync(Guid lotId) =>
            Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.Exists);
    }

    private sealed class FakeReservationRatioPolicyRepository : IReservationRatioPolicyRepository
    {
        public List<ReservationRatioPolicy> Policies { get; } = new();

        public Task<ReservationRatioPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Policies.FirstOrDefault(p => p.LotId == lotId));
        }

        public Task AddAsync(ReservationRatioPolicy policy, CancellationToken cancellationToken = default)
        {
            Policies.Add(policy);
            return Task.CompletedTask;
        }

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ReservationRatioPolicy> UpsertAsync(Guid lotId, int ratioPercent, CancellationToken cancellationToken = default)
        {
            var existing = Policies.FirstOrDefault(p => p.LotId == lotId);
            if (existing != null)
            {
                existing.Update(ratioPercent);
                return Task.FromResult(existing);
            }

            var created = ReservationRatioPolicy.Create(lotId, ratioPercent);
            Policies.Add(created);
            return Task.FromResult(created);
        }
    }
}
