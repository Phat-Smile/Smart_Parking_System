using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Domain;

public sealed class AuditLogTests
{
    [Fact]
    public void CreateFromPayment_WithValidData_CreatesAuditLogSuccessfully()
    {
        var id = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var recordedAt = DateTime.UtcNow;
        var actorId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var log = AuditLog.CreateFromPayment(
            id: id,
            occurredAtUtc: occurredAt,
            recordedAtUtc: recordedAt,
            actionType: "CashPaymentConfirmed",
            actorId: actorId,
            actorEmail: "staff@example.com",
            actorRole: "Staff",
            parkingLotId: lotId,
            targetType: "ParkingSession",
            targetId: "session-123",
            reason: "  Thu tien mat tai cong  ",
            ipAddress: "127.0.0.1",
            details: "{\"amount\":50000}");

        Assert.Equal(id, log.Id);
        Assert.Equal(id, log.SourceEntryId);
        Assert.Equal("payment", log.SourceService);
        Assert.Equal(occurredAt, log.OccurredAtUtc);
        Assert.Equal(recordedAt, log.RecordedAtUtc);
        Assert.Equal("CashPaymentConfirmed", log.ActionType);
        Assert.Equal(actorId, log.ActorId);
        Assert.Equal("staff@example.com", log.ActorEmail);
        Assert.Equal("Staff", log.ActorRole);
        Assert.Equal(lotId, log.ParkingLotId);
        Assert.Equal("ParkingSession", log.TargetType);
        Assert.Equal("session-123", log.TargetId);
        Assert.Equal("Thu tien mat tai cong", log.Reason);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("{\"amount\":50000}", log.Details);
    }

    [Fact]
    public void CreateFromPayment_WithEmptyRole_DefaultsToSystemRole()
    {
        var log = AuditLog.CreateFromPayment(
            id: Guid.NewGuid(),
            occurredAtUtc: DateTime.UtcNow,
            recordedAtUtc: DateTime.UtcNow,
            actionType: "CashPaymentConfirmed",
            actorId: null,
            actorEmail: null,
            actorRole: "",
            parkingLotId: null,
            targetType: "ParkingSession",
            targetId: "session-123",
            reason: "Automated check",
            ipAddress: null,
            details: null);

        Assert.Equal("System", log.ActorRole);
        Assert.Null(log.ActorId);
        Assert.Null(log.ActorEmail);
    }

    [Fact]
    public void CreateFromPayment_WithEmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.CreateFromPayment(
                id: Guid.Empty,
                occurredAtUtc: DateTime.UtcNow,
                recordedAtUtc: DateTime.UtcNow,
                actionType: "CashPaymentConfirmed",
                actorId: null,
                actorEmail: null,
                actorRole: "Staff",
                parkingLotId: null,
                targetType: "ParkingSession",
                targetId: "session-123",
                reason: "Valid reason",
                ipAddress: null,
                details: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromPayment_WithEmptyActionType_ThrowsArgumentException(string? actionType)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.CreateFromPayment(
                id: Guid.NewGuid(),
                occurredAtUtc: DateTime.UtcNow,
                recordedAtUtc: DateTime.UtcNow,
                actionType: actionType!,
                actorId: null,
                actorEmail: null,
                actorRole: "Staff",
                parkingLotId: null,
                targetType: "ParkingSession",
                targetId: "session-123",
                reason: "Valid reason",
                ipAddress: null,
                details: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromPayment_WithEmptyReason_ThrowsArgumentException(string? reason)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.CreateFromPayment(
                id: Guid.NewGuid(),
                occurredAtUtc: DateTime.UtcNow,
                recordedAtUtc: DateTime.UtcNow,
                actionType: "CashPaymentConfirmed",
                actorId: null,
                actorEmail: null,
                actorRole: "Staff",
                parkingLotId: null,
                targetType: "ParkingSession",
                targetId: "session-123",
                reason: reason!,
                ipAddress: null,
                details: null));
    }

    [Fact]
    public void CreateFromPayment_WithReasonExceeding500Chars_ThrowsArgumentException()
    {
        var longReason = new string('A', 501);

        Assert.Throws<ArgumentException>(() =>
            AuditLog.CreateFromPayment(
                id: Guid.NewGuid(),
                occurredAtUtc: DateTime.UtcNow,
                recordedAtUtc: DateTime.UtcNow,
                actionType: "CashPaymentConfirmed",
                actorId: null,
                actorEmail: null,
                actorRole: "Staff",
                parkingLotId: null,
                targetType: "ParkingSession",
                targetId: "session-123",
                reason: longReason,
                ipAddress: null,
                details: null));
    }
}
