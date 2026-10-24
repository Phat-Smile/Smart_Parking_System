using ParkingSystem.Parking.Domain.ParkingLayout;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingLayoutChangeRequestTests
{
    private static ParkingLayoutChangeRequest NewRequest() =>
        ParkingLayoutChangeRequest.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3,
            "[]", DateTimeOffset.UtcNow);

    [Fact]
    public void Submitted_request_is_pending_and_preserves_expected_active_version()
    {
        var request = NewRequest();
        Assert.Equal(ParkingLayoutChangeStatus.Pending, request.Status);
        Assert.Equal(3, request.ExpectedVersion);
        Assert.Null(request.ReviewedBy);
    }

    [Fact]
    public void Approved_request_cannot_be_reviewed_twice()
    {
        var request = NewRequest();
        Assert.True(request.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(ParkingLayoutChangeStatus.Approved, request.Status);
        Assert.False(request.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.False(request.Reject(Guid.NewGuid(), "reason", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rejected_request_records_reason_and_cannot_be_approved()
    {
        var request = NewRequest();
        Assert.True(request.Reject(Guid.NewGuid(), " not suitable ", DateTimeOffset.UtcNow));
        Assert.Equal(ParkingLayoutChangeStatus.Rejected, request.Status);
        Assert.Equal("not suitable", request.RejectionReason);
        Assert.False(request.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rejection_without_a_reason_is_invalid()
    {
        var request = NewRequest();
        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), "  ", DateTimeOffset.UtcNow));
        Assert.Equal(ParkingLayoutChangeStatus.Pending, request.Status);
    }
}
