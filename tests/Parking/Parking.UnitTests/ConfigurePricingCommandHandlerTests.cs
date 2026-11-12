using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ConfigurePricingCommandHandlerTests
{
    private readonly FakePricingPolicyRepository _pricingRepository = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly FakeLotOwnershipService _lotOwnershipService = new();
    private readonly ConfigurePricingCommandHandler _handler;

    public ConfigurePricingCommandHandlerTests()
    {
        _handler = new ConfigurePricingCommandHandler(
            _pricingRepository,
            _auditWriter,
            _lotOwnershipService);
    }

    [Fact]
    public async Task Handle_WhenLotDoesNotExist_ShouldReturnLotNotFoundError()
    {
        // Arrange: Bai xe khong ton tai trong he thong
        var lotId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: false, isOwner: false);

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 10000m,
            OvernightFee = 50000m,
            ExpectedVersion = null,
            Reason = string.Empty,
            CallerId = callerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.LotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotLotOwner_ShouldReturnNotLotOwnerError_AndPreventIdor()
    {
        // Arrange: Bai xe ton tai nhung callerId khong phai la Owner (chong lo hong IDOR)
        var lotId = Guid.NewGuid();
        var attackerCallerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: false);

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 10000m,
            OvernightFee = 50000m,
            ExpectedVersion = null,
            Reason = string.Empty,
            CallerId = attackerCallerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.NotLotOwner.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenExpectedVersionMismatches_ShouldReturnVersionConflictError()
    {
        // Arrange: Phien ban ky vong khong khop voi phien ban hien tai
        var lotId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var existingPolicy = PricingPolicy.Create(lotId, "CAR", 1, "Block", 30, 10000m, 50000m);
        _pricingRepository.Policies.Add(existingPolicy);

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 12000m,
            OvernightFee = 60000m,
            ExpectedVersion = 0, // Ky vong V0 nhung hien tai la V1
            Reason = "Tang gia",
            CallerId = ownerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ParkingErrors.Pricing.VersionConflict.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenValuesUnchanged_ShouldReturnSuccessWithoutCreatingNewVersion()
    {
        // Arrange: Cac gia tri cau hinh y het phien ban hien tai
        var lotId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var existingPolicy = PricingPolicy.Create(lotId, "CAR", 1, "Block", 30, 10000m, 50000m);
        _pricingRepository.Policies.Add(existingPolicy);

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 10000m,
            OvernightFee = 50000m,
            ExpectedVersion = 1,
            Reason = "Luu lai khong doi",
            CallerId = ownerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
        Assert.False(result.Value.IsNewVersionCreated);
        Assert.Single(_pricingRepository.Policies);
    }

    [Fact]
    public async Task Handle_WhenValidAndValuesChanged_ShouldCreateNewVersionAndLogAudit()
    {
        // Arrange: Chu bai hop le, gia tri moi, tao version 2
        var lotId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _lotOwnershipService.SetupLot(lotId, exists: true, isOwner: true);

        var existingPolicy = PricingPolicy.Create(lotId, "CAR", 1, "Block", 30, 10000m, 50000m);
        _pricingRepository.Policies.Add(existingPolicy);

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 15000m,
            OvernightFee = 70000m,
            ExpectedVersion = 1,
            Reason = "Cap nhat bang gia dip le",
            CallerId = ownerId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Version);
        Assert.True(result.Value.IsNewVersionCreated);
        Assert.Equal(2, _pricingRepository.Policies.Count);
        Assert.Single(_auditWriter.Logs);
    }

    private sealed class FakeLotOwnershipService : ILotOwnershipService
    {
        private readonly Dictionary<Guid, (bool Exists, bool IsOwner)> _lots = new();

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

        // Setting a price does not depend on the approval of the lot; an owner prices a Pending lot too.
        public Task<bool> IsLotApprovedAsync(Guid lotId)
        {
            return Task.FromResult(_lots.TryGetValue(lotId, out var lot) && lot.Exists);
        }
    }

    private sealed class FakePricingPolicyRepository : IPricingPolicyRepository
    {
        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public List<PricingPolicy> Policies { get; } = new();

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

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<(Guid ActorId, string Action, string TargetId, string Details)> Logs { get; } = new();

        public void Log(Guid actorId, string action, string targetId, string details)
        {
            Logs.Add((actorId, action, targetId, details));
        }
    }
}
