using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationLimitsAndOverlapDomainTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    private static Reservation CreateSampleReservation(
        Guid? id = null,
        Guid? userId = null,
        string plateNumber = "51F-123.45",
        ReservationStatus status = ReservationStatus.TemporaryHold,
        DateTimeOffset? appointmentTime = null,
        int? durationMinutes = 60,
        DateTimeOffset? tempHoldExpiresAt = null,
        DateTimeOffset? windowStartAt = null,
        DateTimeOffset? windowEndAt = null)
    {
        var appTime = appointmentTime ?? BaseTime;
        var start = windowStartAt ?? appTime.AddMinutes(-30);
        var end = windowEndAt ?? appTime.AddMinutes(durationMinutes ?? 60);
        var holdEnd = appTime.AddMinutes(15);
        var expiresAt = tempHoldExpiresAt ?? BaseTime.AddMinutes(10);

        var res = Reservation.CreateTemporaryHold(
            id: id ?? Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: plateNumber,
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: appTime,
            prepaidAmount: 20000m,
            createdAt: BaseTime,
            durationMinutes: durationMinutes,
            tempHoldExpiresAt: expiresAt,
            windowStartAt: start,
            windowEndAt: end,
            holdEndAt: holdEnd);

        if (status == ReservationStatus.Confirmed)
        {
            res.Confirm(BaseTime.AddMinutes(1));
        }
        else if (status == ReservationStatus.Used)
        {
            res.Confirm(BaseTime.AddMinutes(1));
            res.Use(BaseTime.AddMinutes(5), Guid.NewGuid());
        }
        else if (status == ReservationStatus.Cancelled)
        {
            res.Cancel(BaseTime.AddMinutes(2), CancelReason.CustomerCancelled);
        }
        else if (status == ReservationStatus.Expired)
        {
            res.Expire();
        }

        return res;
    }

    // =========================================================================
    // BR-1: Reservation Limits & Active Status Checks (RL-01 to RL-10)
    // =========================================================================

    [Fact]
    public void RL_Default_Max_Active_Reservations_Per_User_Is_Three()
    {
        Assert.Equal(3, Reservation.MaxActiveReservationsPerUser);
    }

    [Fact]
    public void RL_01_TemporaryHold_With_Future_Expiry_Is_Active()
    {
        var now = BaseTime;
        var reservation = CreateSampleReservation(
            status: ReservationStatus.TemporaryHold,
            tempHoldExpiresAt: now.AddMinutes(5));

        Assert.True(reservation.IsActive(now));
    }

    [Fact]
    public void RL_02_TemporaryHold_With_Past_Or_Equal_Expiry_Is_Not_Active()
    {
        var now = BaseTime;
        var expiredHold = CreateSampleReservation(
            status: ReservationStatus.TemporaryHold,
            tempHoldExpiresAt: now.AddMinutes(-1));

        var exactHold = CreateSampleReservation(
            status: ReservationStatus.TemporaryHold,
            tempHoldExpiresAt: now);

        Assert.False(expiredHold.IsActive(now));
        Assert.False(exactHold.IsActive(now));
    }

    [Fact]
    public void RL_03_Confirmed_Reservation_Is_Always_Active()
    {
        var now = BaseTime;
        var confirmed = CreateSampleReservation(status: ReservationStatus.Confirmed);

        Assert.True(confirmed.IsActive(now));
        Assert.True(confirmed.IsActive(now.AddHours(5)));
    }

    [Theory]
    [InlineData(ReservationStatus.Used)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Expired)]
    public void RL_05_RL_06_NonActive_Statuses_Are_Never_Active(ReservationStatus status)
    {
        var now = BaseTime;
        var reservation = CreateSampleReservation(status: status);

        Assert.False(reservation.IsActive(now));
    }

    [Fact]
    public void RL_07_RL_08_Active_Count_Filters_Properly_For_User()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = BaseTime;

        var userList = new List<Reservation>
        {
            CreateSampleReservation(userId: userId, status: ReservationStatus.Confirmed),
            CreateSampleReservation(userId: userId, status: ReservationStatus.TemporaryHold, tempHoldExpiresAt: now.AddMinutes(5)),
            CreateSampleReservation(userId: userId, status: ReservationStatus.Cancelled),
            CreateSampleReservation(userId: userId, status: ReservationStatus.Used),
            CreateSampleReservation(userId: userId, status: ReservationStatus.TemporaryHold, tempHoldExpiresAt: now.AddMinutes(-5)), // expired hold
            CreateSampleReservation(userId: otherUserId, status: ReservationStatus.Confirmed) // other user
        };

        var activeCountForUser = userList.Count(r => r.UserId == userId && r.IsActive(now));
        var activeCountForOther = userList.Count(r => r.UserId == otherUserId && r.IsActive(now));

        Assert.Equal(2, activeCountForUser);
        Assert.Equal(1, activeCountForOther);
    }

    // =========================================================================
    // BR-2: Plate Overlap & Half-Open Interval Semantics (PO-01 to PO-12)
    // =========================================================================

    [Fact]
    public void PO_11_Plate_Number_Is_Normalized_To_Uppercase_And_Trimmed()
    {
        var res1 = CreateSampleReservation(plateNumber: " 51f-123.45 ");
        Assert.Equal("51F-123.45", res1.PlateNumber);

        var res2 = CreateSampleReservation(plateNumber: "29A-99999");
        Assert.Equal("29A-99999", res2.PlateNumber);
    }

    [Fact]
    public void PO_01_Non_Overlapping_Windows_Do_Not_Overlap()
    {
        // Window A: [08:00, 09:00)
        // Window B: [10:00, 11:00)
        var startA = BaseTime.AddHours(8);
        var endA = BaseTime.AddHours(9);
        var startB = BaseTime.AddHours(10);
        var endB = BaseTime.AddHours(11);

        var resA = CreateSampleReservation(windowStartAt: startA, windowEndAt: endA);

        Assert.False(resA.OverlapsWith(startB, endB));
        Assert.False(ReservationTimeWindow.IsOverlapping(startA, endA, startB, endB));
    }

    [Fact]
    public void PO_02_Identical_Windows_Overlap()
    {
        // Window A: [10:00, 12:00)
        // Window B: [10:00, 12:00)
        var start = BaseTime.AddHours(10);
        var end = BaseTime.AddHours(12);

        var resA = CreateSampleReservation(windowStartAt: start, windowEndAt: end);

        Assert.True(resA.OverlapsWith(start, end));
    }

    [Fact]
    public void PO_03_New_Window_Contained_Inside_Existing_Overlaps()
    {
        // Existing: [09:00, 12:00)
        // New:      [10:00, 11:00)
        var existingStart = BaseTime.AddHours(9);
        var existingEnd = BaseTime.AddHours(12);
        var newStart = BaseTime.AddHours(10);
        var newEnd = BaseTime.AddHours(11);

        var existing = CreateSampleReservation(windowStartAt: existingStart, windowEndAt: existingEnd);

        Assert.True(existing.OverlapsWith(newStart, newEnd));
    }

    [Fact]
    public void PO_04_New_Window_Starting_Inside_Existing_Overlaps()
    {
        // Existing: [09:00, 11:00)
        // New:      [10:00, 12:00)
        var existingStart = BaseTime.AddHours(9);
        var existingEnd = BaseTime.AddHours(11);
        var newStart = BaseTime.AddHours(10);
        var newEnd = BaseTime.AddHours(12);

        var existing = CreateSampleReservation(windowStartAt: existingStart, windowEndAt: existingEnd);

        Assert.True(existing.OverlapsWith(newStart, newEnd));
    }

    [Fact]
    public void PO_05_New_Window_Ending_Inside_Existing_Overlaps()
    {
        // Existing: [10:00, 12:00)
        // New:      [09:00, 11:00)
        var existingStart = BaseTime.AddHours(10);
        var existingEnd = BaseTime.AddHours(12);
        var newStart = BaseTime.AddHours(9);
        var newEnd = BaseTime.AddHours(11);

        var existing = CreateSampleReservation(windowStartAt: existingStart, windowEndAt: existingEnd);

        Assert.True(existing.OverlapsWith(newStart, newEnd));
    }

    [Fact]
    public void PO_06_Adjacent_Windows_Under_Half_Open_Semantics_Do_Not_Overlap()
    {
        // Interval A: [08:00, 09:00)
        // Interval B: [09:00, 10:00)
        // Boundary condition: endA == startB -> no overlap
        var startA = BaseTime.AddHours(8);
        var boundary = BaseTime.AddHours(9);
        var endB = BaseTime.AddHours(10);

        var resA = CreateSampleReservation(windowStartAt: startA, windowEndAt: boundary);

        Assert.False(resA.OverlapsWith(boundary, endB));
        Assert.False(ReservationTimeWindow.IsOverlapping(startA, boundary, boundary, endB));
    }

    [Fact]
    public void PO_10_Invalid_Interval_Throws_ArgumentException()
    {
        var start = BaseTime.AddHours(10);
        var invalidEnd = BaseTime.AddHours(9); // end <= start

        Assert.Throws<ArgumentException>(() =>
            ReservationTimeWindow.IsOverlapping(start, invalidEnd, BaseTime, BaseTime.AddHours(1)));
    }
}
