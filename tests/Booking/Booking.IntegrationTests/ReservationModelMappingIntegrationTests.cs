using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationModelMappingIntegrationTests
{
    [Fact]
    public void BookingDbContext_maps_reservations_and_reservation_capacity_to_booking_schema()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Database=smart_parking_test;Username=postgres;Password=postgres")
            .Options;

        using var context = new BookingDbContext(options);
        var model = context.Model;

        // Verify Reservation table mapping against Booking.txt ERD
        var reservationEntity = model.FindEntityType(typeof(Reservation));
        Assert.NotNull(reservationEntity);
        Assert.Equal("reservations", reservationEntity.GetTableName());
        Assert.Equal("booking", reservationEntity.GetSchema());

        var pk = reservationEntity.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal("id", pk.Properties[0].GetColumnName());

        // Check columns from Booking.txt
        Assert.NotNull(reservationEntity.FindProperty("UserId"));
        Assert.Equal("user_id", reservationEntity.FindProperty("UserId")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("VehicleId"));
        Assert.Equal("vehicle_id", reservationEntity.FindProperty("VehicleId")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("PlateNumber"));
        Assert.Equal("plate_number", reservationEntity.FindProperty("PlateNumber")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("LotId"));
        Assert.Equal("lot_id", reservationEntity.FindProperty("LotId")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("VehicleGroup"));
        Assert.Equal("vehicle_group", reservationEntity.FindProperty("VehicleGroup")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("ReservationType"));
        Assert.Equal("reservation_type", reservationEntity.FindProperty("ReservationType")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("Status"));
        Assert.Equal("status", reservationEntity.FindProperty("Status")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("AppointmentTime"));
        Assert.Equal("appointment_time", reservationEntity.FindProperty("AppointmentTime")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("DurationMinutes"));
        Assert.Equal("duration_minutes", reservationEntity.FindProperty("DurationMinutes")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("TempHoldExpiresAt"));
        Assert.Equal("temp_hold_expires_at", reservationEntity.FindProperty("TempHoldExpiresAt")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("PrepaidAmount"));
        Assert.Equal("prepaid_amount", reservationEntity.FindProperty("PrepaidAmount")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("PriceSnapshot"));
        Assert.Equal("price_snapshot", reservationEntity.FindProperty("PriceSnapshot")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("ConfirmedAt"));
        Assert.Equal("confirmed_at", reservationEntity.FindProperty("ConfirmedAt")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("UsedAt"));
        Assert.Equal("used_at", reservationEntity.FindProperty("UsedAt")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("SessionId"));
        Assert.Equal("session_id", reservationEntity.FindProperty("SessionId")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("CancelledAt"));
        Assert.Equal("cancelled_at", reservationEntity.FindProperty("CancelledAt")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("CancelReason"));
        Assert.Equal("cancel_reason", reservationEntity.FindProperty("CancelReason")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("PrepaidOutcome"));
        Assert.Equal("prepaid_outcome", reservationEntity.FindProperty("PrepaidOutcome")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("IsNoShow"));
        Assert.Equal("is_no_show", reservationEntity.FindProperty("IsNoShow")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("RowVersion"));
        Assert.Equal("row_version", reservationEntity.FindProperty("RowVersion")!.GetColumnName());

        Assert.NotNull(reservationEntity.FindProperty("CreatedAt"));
        Assert.Equal("created_at", reservationEntity.FindProperty("CreatedAt")!.GetColumnName());

        // Verify ReservationCapacity table mapping against Booking.txt ERD
        var capacityEntity = model.FindEntityType(typeof(ReservationCapacity));
        Assert.NotNull(capacityEntity);
        Assert.Equal("reservation_capacity", capacityEntity.GetTableName());
        Assert.Equal("booking", capacityEntity.GetSchema());

        var capacityPk = capacityEntity.FindPrimaryKey();
        Assert.NotNull(capacityPk);
        Assert.Equal(2, capacityPk.Properties.Count);
        Assert.Contains(capacityPk.Properties, p => p.GetColumnName() == "lot_id");
        Assert.Contains(capacityPk.Properties, p => p.GetColumnName() == "vehicle_group");
    }
}
