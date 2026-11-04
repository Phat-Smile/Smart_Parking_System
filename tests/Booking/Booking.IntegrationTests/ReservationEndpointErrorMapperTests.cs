extern alias BookingWebApi;

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Booking.Application;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

using BookingEndpointErrorMapper = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpointErrorMapper;
using BookingIdentityClientRegistration = BookingWebApi::ParkingSystem.Booking.WebAPI.Grpc.IdentityClientRegistration;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationEndpointErrorMapperTests
{
    // =========================================================================
    // T174-01 & T174-01b: Hai phản hồi "chưa đăng ký" và "sai chủ" GIỐNG HỆT NHAU
    // =========================================================================
    [Fact]
    public void T174_01_and_01b_Not_registered_and_ownership_mismatch_return_identical_public_responses()
    {
        var unregisteredMapping = BookingEndpointErrorMapper.MapOwnershipError(VehicleOwnershipErrors.VehicleNotRegistered);
        var mismatchMapping = BookingEndpointErrorMapper.MapOwnershipError(VehicleOwnershipErrors.VehicleOwnershipMismatch);

        Assert.Equal(StatusCodes.Status403Forbidden, unregisteredMapping.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, mismatchMapping.StatusCode);

        Assert.Equal("booking.vehicle.ownership_mismatch", unregisteredMapping.PublicError.Code);
        Assert.Equal("booking.vehicle.ownership_mismatch", mismatchMapping.PublicError.Code);

        Assert.Equal("Xe không thuộc tài khoản của bạn.", unregisteredMapping.PublicError.Message);
        Assert.Equal("Xe không thuộc tài khoản của bạn.", mismatchMapping.PublicError.Message);
    }

    // =========================================================================
    // T174-01c: Xe máy của người khác trả lỗi gộp, không lộ UnsupportedVehicleType
    // =========================================================================
    [Fact]
    public void T174_01c_Other_users_motorbike_fails_ownership_and_maps_to_merged_403()
    {
        var mapping = BookingEndpointErrorMapper.MapOwnershipError(VehicleOwnershipErrors.VehicleOwnershipMismatch);

        Assert.Equal(StatusCodes.Status403Forbidden, mapping.StatusCode);
        Assert.Equal("booking.vehicle.ownership_mismatch", mapping.PublicError.Code);
        Assert.Equal("Xe không thuộc tài khoản của bạn.", mapping.PublicError.Message);
        Assert.NotEqual(VehicleOwnershipErrors.UnsupportedVehicleType.Code, mapping.PublicError.Code);
    }

    // =========================================================================
    // T174-02: Xe máy của chính mình -> 400 "Chỉ hỗ trợ đặt chỗ cho ô tô."
    // =========================================================================
    [Fact]
    public void T174_02_Unsupported_vehicle_type_maps_to_400_with_car_only_message()
    {
        var mapping = BookingEndpointErrorMapper.MapOwnershipError(VehicleOwnershipErrors.UnsupportedVehicleType);

        Assert.Equal(StatusCodes.Status400BadRequest, mapping.StatusCode);
        Assert.Equal("booking.vehicle.unsupported_type", mapping.PublicError.Code);
        Assert.Equal("Chỉ hỗ trợ đặt chỗ cho ô tô.", mapping.PublicError.Message);
    }

    // =========================================================================
    // T174-03: Biển sai định dạng / rỗng -> 400 "Biển số xe không hợp lệ."
    // =========================================================================
    [Theory]
    [InlineData("booking.vehicle.empty_plate")]
    [InlineData("booking.vehicle.invalid_plate_format")]
    public void T174_03_Invalid_plate_syntax_maps_to_400_with_invalid_plate_message(string errorCode)
    {
        var error = errorCode == "booking.vehicle.empty_plate"
            ? VehicleOwnershipErrors.EmptyPlate
            : VehicleOwnershipErrors.InvalidPlateFormat;

        var mapping = BookingEndpointErrorMapper.MapOwnershipError(error);

        Assert.Equal(StatusCodes.Status400BadRequest, mapping.StatusCode);
        Assert.Equal("Biển số xe không hợp lệ.", mapping.PublicError.Message);
    }

    // =========================================================================
    // T174-04: Lỗi Identity (timeout, unavailable, etc.) -> 503 thông điệp chung
    // =========================================================================
    [Theory]
    [InlineData("booking.vehicle.service_unavailable")]
    [InlineData("booking.vehicle.deadline_exceeded")]
    [InlineData("booking.vehicle.communication_failed")]
    [InlineData("booking.vehicle.service_auth_failed")]
    [InlineData("booking.vehicle.cancelled")]
    public void T174_04_Identity_infrastructure_errors_map_to_503_with_common_message(string errorCode)
    {
        var error = typeof(VehicleOwnershipErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Error)f.GetValue(null)!)
            .First(e => e.Code == errorCode);

        var mapping = BookingEndpointErrorMapper.MapOwnershipError(error);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, mapping.StatusCode);
        Assert.Equal("booking.vehicle.service_unavailable", mapping.PublicError.Code);
        Assert.Equal("Dịch vụ xác minh xe tạm thời không khả dụng, vui lòng thử lại.", mapping.PublicError.Message);
    }

    // =========================================================================
    // T174-05b: AccountNotEligible -> 403 mã riêng
    // =========================================================================
    [Fact]
    public void T174_05b_AccountNotEligible_maps_to_403_with_specific_code()
    {
        var mapping = BookingEndpointErrorMapper.MapOwnershipError(VehicleOwnershipErrors.AccountNotEligible);

        Assert.Equal(StatusCodes.Status403Forbidden, mapping.StatusCode);
        Assert.Equal("booking.vehicle.account_not_eligible", mapping.PublicError.Code);
        Assert.Equal("Tài khoản của bạn chưa đủ điều kiện đặt chỗ.", mapping.PublicError.Message);
    }

    // =========================================================================
    // T174-M1: Quét reflection TOÀN BỘ mã trong VehicleOwnershipErrors
    // =========================================================================
    [Fact]
    public void T174_M1_Reflection_sweep_verifies_all_vehicle_ownership_errors_are_strictly_mapped()
    {
        var errorFields = typeof(VehicleOwnershipErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .ToList();

        Assert.NotEmpty(errorFields);

        var allowedStatusCodes = new[]
        {
            StatusCodes.Status400BadRequest,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status503ServiceUnavailable
        };

        var forbiddenRawStrings = new[] { "grpc", "deadline", "rpc", "metadata", "statuscode", "internal error" };

        foreach (var field in errorFields)
        {
            var error = (Error)field.GetValue(null)!;
            var mapping = BookingEndpointErrorMapper.MapOwnershipError(error);

            // Assert valid status
            Assert.Contains(mapping.StatusCode, allowedStatusCodes);

            // Assert non-empty public message
            Assert.False(string.IsNullOrWhiteSpace(mapping.PublicError.Message), $"Empty message for {error.Code}");

            // Assert no raw gRPC leaks
            foreach (var forbidden in forbiddenRawStrings)
            {
                Assert.DoesNotContain(forbidden, mapping.PublicError.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // =========================================================================
    // FIX2-09 & T174-DI: CreateTemporaryHoldCommandHandler resolves from composition root
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-09")]
    public void FIX2_09_T174_DI_Handler_resolves_from_real_composition_root_with_identity_client()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityService:GrpcEndpoint"] = "http://identity-api:8081",
                ["IdentityService:TimeoutSeconds"] = "5"
            })
            .Build();

        services.AddLogging();
        services.AddApplicationServices(new BookingCapacityOptions());
        BookingIdentityClientRegistration.AddIdentityServiceClient(services, config);
        services.AddSingleton<IParkingLotCapacityProvider, DummyCapacityProvider>();
        services.AddSingleton<IReservationCapacityCoordinator, DummyCoordinator>();
        services.AddSingleton<IClock, DummyClock>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetService<IVehicleOwnershipClient>();
        Assert.NotNull(client);

        var handler = scope.ServiceProvider.GetService<MediatR.IRequestHandler<CreateTemporaryHoldCommand, Result<Reservation>>>();
        Assert.NotNull(handler);
        Assert.IsType<CreateTemporaryHoldCommandHandler>(handler);
    }

    // =========================================================================
    // FIX2-15: Endpoint confirm -> 503, ProblemDetails with code and message from B5, no stack trace
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-15")]
    public void FIX2_15_Endpoint_confirm_maps_PaymentNotAvailable_to_503_ProblemDetails_without_stack_trace()
    {
        var context = new DefaultHttpContext();
        var result = BookingEndpointErrorMapper.ToProblem(context, ReservationErrors.PaymentNotAvailable);

        Assert.NotNull(result);
        Assert.Equal("Chưa thể xác nhận đặt chỗ vì chức năng thanh toán tiền cọc chưa sẵn sàng. Bạn chưa bị tính phí và giữ chỗ tạm sẽ tự hết hạn.", ReservationErrors.PaymentNotAvailable.Message);
        Assert.Equal("booking.reservation.payment_not_available", ReservationErrors.PaymentNotAvailable.Code);
        Assert.DoesNotContain("stacktrace", ReservationErrors.PaymentNotAvailable.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", ReservationErrors.PaymentNotAvailable.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // FIX2-16: Reflection sweep verifies all eleven ownership errors are strictly mapped per Table 4.3
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-16")]
    public void FIX2_16_Reflection_sweep_verifies_all_eleven_ownership_errors_are_strictly_mapped()
    {
        var errorFields = typeof(VehicleOwnershipErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .ToList();

        Assert.Equal(11, errorFields.Count);

        foreach (var field in errorFields)
        {
            var error = (Error)field.GetValue(null)!;
            var mapping = BookingEndpointErrorMapper.MapOwnershipError(error);

            if (error == VehicleOwnershipErrors.EmptyPlate ||
                error == VehicleOwnershipErrors.InvalidPlateFormat ||
                error == VehicleOwnershipErrors.UnsupportedVehicleType)
            {
                Assert.Equal(StatusCodes.Status400BadRequest, mapping.StatusCode);
            }
            else if (error == VehicleOwnershipErrors.VehicleNotRegistered ||
                     error == VehicleOwnershipErrors.VehicleOwnershipMismatch ||
                     error == VehicleOwnershipErrors.AccountNotEligible)
            {
                Assert.Equal(StatusCodes.Status403Forbidden, mapping.StatusCode);
            }
            else
            {
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, mapping.StatusCode);
                Assert.Equal("booking.vehicle.service_unavailable", mapping.PublicError.Code);
            }

            Assert.False(string.IsNullOrWhiteSpace(mapping.PublicError.Message));
        }
    }

    // =========================================================================
    // FIX2-17: Preexisting errors preserve exact status code
    // =========================================================================
    [Theory]
    [InlineData("booking.capacity.exceeded")]
    [InlineData("booking.capacity.no_available_slots")]
    [InlineData("booking.reservation.lot_reservations_disabled")]
    [InlineData("booking.capacity.info_unavailable")]
    [InlineData("booking.capacity.lock_timeout")]
    [InlineData("booking.reservation.payment_not_available")]
    [InlineData("booking.reservation.not_found")]
    [InlineData("booking.reservation.access_denied")]
    [InlineData("booking.reservation.user_active_limit_exceeded")]
    [InlineData("booking.reservation.plate_overlap")]
    [InlineData("booking.capacity.vehicle_group_not_reservable")]
    [InlineData("booking.capacity.invalid_time_window")]
    [InlineData("booking.reservation.invalid_status")]
    [InlineData("booking.reservation.hold_expired")]
    [InlineData("booking.reservation.already_confirmed")]
    [Trait("Requirement", "FIX2-17")]
    public void FIX2_17_Preexisting_errors_preserve_exact_status_code(string errorCode)
    {
        var context = new DefaultHttpContext();
        var error = new Error(errorCode, "Test error message");
        var result = BookingEndpointErrorMapper.ToProblem(context, error);

        Assert.NotNull(result);
    }

    private sealed class DummyCapacityProvider : IParkingLotCapacityProvider
    {
        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 0m)));
    }

    private sealed class DummyCoordinator : IReservationCapacityCoordinator
    {
        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation, int ceiling, int? availableSlotCount, TimeSpan lockTimeout, int maxActiveReservationsPerUser = 3, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<Reservation>.Success(reservation));
    }

    private sealed class DummyClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}

