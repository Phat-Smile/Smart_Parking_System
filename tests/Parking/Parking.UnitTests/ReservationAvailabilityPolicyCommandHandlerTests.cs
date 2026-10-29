using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;
using ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Queries.GetReservationAvailabilityPolicy;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ReservationAvailabilityPolicyCommandHandlerTests
{
    private readonly FakeReservationAvailabilityPolicyRepository _repository = new();
    private readonly FakeLotOwnershipService _lotOwnershipService = new();
    private readonly ConfigureReservationAvailabilityPolicyCommandHandler _configureHandler;
    private readonly GetReservationAvailabilityPolicyQueryHandler _getHandler;

    public ReservationAvailabilityPolicyCommandHandlerTests()
    {
        _configureHandler = new ConfigureReservationAvailabilityPolicyCommandHandler(_repository, _lotOwnershipService);
        _getHandler = new GetReservationAvailabilityPolicyQueryHandler(_repository, _lotOwnershipService);
    }

    [Fact]
    public async Task Configure_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var command = new ConfigureReservationAvailabilityPolicyCommand(lotId, true, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationAvailabilityPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var command = new ConfigureReservationAvailabilityPolicyCommand(lotId, true, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationAvailabilityPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Configure_WhenValidAndNoExistingPolicy_ShouldCreatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var command = new ConfigureReservationAvailabilityPolicyCommand(lotId, false, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.False(result.Value.ReservationEnabled);
        Assert.True(result.Value.IsConfigured);
        Assert.Single(_repository.Policies);
        Assert.False(_repository.Policies[0].ReservationEnabled);
    }

    [Fact]
    public async Task Configure_WhenValidAndExistingPolicyExists_ShouldUpdatePolicy()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var initialPolicy = ReservationAvailabilityPolicy.Create(lotId, true);
        await _repository.AddAsync(initialPolicy);

        var command = new ConfigureReservationAvailabilityPolicyCommand(lotId, false, callerId);
        var result = await _configureHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.ReservationEnabled);
        Assert.True(result.Value.IsConfigured);
        Assert.Single(_repository.Policies);
        Assert.False(_repository.Policies[0].ReservationEnabled);
    }

    [Fact]
    public async Task Configure_ToggleOffThenOn_ShouldPersistCorrectlyWithoutAddingNewRow()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        // Turn OFF
        var cmd1 = new ConfigureReservationAvailabilityPolicyCommand(lotId, false, callerId);
        var res1 = await _configureHandler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.False(res1.Value!.ReservationEnabled);
        Assert.Single(_repository.Policies);

        // Turn ON
        var cmd2 = new ConfigureReservationAvailabilityPolicyCommand(lotId, true, callerId);
        var res2 = await _configureHandler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.True(res2.Value!.ReservationEnabled);
        Assert.Single(_repository.Policies);
        Assert.True(_repository.Policies[0].ReservationEnabled);
    }

    [Fact]
    public async Task Get_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var query = new GetReservationAvailabilityPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationAvailabilityPolicy.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var query = new GetReservationAvailabilityPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingErrors.ReservationAvailabilityPolicy.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Get_WhenNoPolicyConfigured_ShouldReturnDefaultTrueAndNotConfiguredAndNotWriteDB()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var query = new GetReservationAvailabilityPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.True(result.Value.ReservationEnabled);
        Assert.False(result.Value.IsConfigured);
        Assert.Null(result.Value.UpdatedAtUtc);
        Assert.Empty(_repository.Policies); // Did NOT write to repository!
    }

    [Fact]
    public async Task Get_WhenPolicyConfigured_ShouldReturnConfiguredValueAndIsConfiguredTrue()
    {
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var policy = ReservationAvailabilityPolicy.Create(lotId, false);
        await _repository.AddAsync(policy);

        var query = new GetReservationAvailabilityPolicyQuery(lotId, callerId);
        var result = await _getHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(lotId, result.Value!.LotId);
        Assert.False(result.Value.ReservationEnabled);
        Assert.True(result.Value.IsConfigured);
        Assert.NotNull(result.Value.UpdatedAtUtc);
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

    private sealed class FakeReservationAvailabilityPolicyRepository : IReservationAvailabilityPolicyRepository
    {
        public List<ReservationAvailabilityPolicy> Policies { get; } = new();

        public Task<ReservationAvailabilityPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            var policy = Policies.FirstOrDefault(p => p.LotId == lotId);
            return Task.FromResult(policy);
        }

        public Task AddAsync(ReservationAvailabilityPolicy policy, CancellationToken cancellationToken = default)
        {
            Policies.Add(policy);
            return Task.CompletedTask;
        }

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ReservationAvailabilityPolicy> UpsertAsync(Guid lotId, bool reservationEnabled, CancellationToken cancellationToken = default)
        {
            var existing = Policies.FirstOrDefault(p => p.LotId == lotId);
            if (existing != null)
            {
                existing.Update(reservationEnabled);
                return Task.FromResult(existing);
            }

            var newPolicy = ReservationAvailabilityPolicy.Create(lotId, reservationEnabled);
            Policies.Add(newPolicy);
            return Task.FromResult(newPolicy);
        }
    }
}
