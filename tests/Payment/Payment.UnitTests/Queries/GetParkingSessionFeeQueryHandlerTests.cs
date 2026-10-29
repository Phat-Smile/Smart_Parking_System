using System;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Queries;

public class GetParkingSessionFeeQueryHandlerTests
{
    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class FakeParkingSessionGateway : IParkingSessionGateway
    {
        public Result<ParkingSessionFeeContext>? ResultToReturn { get; set; }
        public Guid? LastSessionId { get; private set; }
        public Guid? LastCallerAccountId { get; private set; }
        public string? LastCallerRole { get; private set; }

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
            Guid sessionId,
            Guid? callerAccountId,
            string callerRole,
            CancellationToken cancellationToken = default)
        {
            LastSessionId = sessionId;
            LastCallerAccountId = callerAccountId;
            LastCallerRole = callerRole;

            return Task.FromResult(ResultToReturn
                ?? Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound));
        }
    }

    private sealed class FakePrepaymentReader : IPrepaymentReader
    {
        public long AmountToReturn { get; set; }
        public bool WasCalled { get; private set; }

        public Task<long> GetPrepaidAmountAsync(
            Guid accountId,
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(AmountToReturn);
        }
    }

    private readonly DateTimeOffset _entryTime = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _calcTime = new(2026, 10, 19, 10, 15, 0, TimeSpan.Zero);
    private readonly Guid _validSessionId = Guid.Parse("3f6c2a52-1d8e-4c55-9d1b-6a7f0c1b2e44");
    private readonly Guid _accountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _bookingId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private ParkingSessionFeeContext CreateDefaultContext(
        string status = "Active",
        Guid? bookingId = null,
        Guid? accountId = null)
    {
        return new ParkingSessionFeeContext(
            SessionId: _validSessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: status,
            EntryTimeUtc: _entryTime,
            AccountId: accountId ?? _accountId,
            BookingId: bookingId,
            Pricing: new PricingSnapshotDto(
                PolicyId: "b1d0c6a4-7f3e-4a0b-8d2c-5e9f1a2b3c4d",
                CalculationMethod: "Block",
                BlockMinutes: 30,
                PricePerUnit: 8000));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public async Task Handle_InvalidSessionId_ReturnsSessionIdInvalidError(string invalidId)
    {
        // TC-FEE-14: Mã phiên đỗ không đúng chuẩn GUID trả về lỗi SessionIdInvalid
        var gateway = new FakeParkingSessionGateway();
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(invalidId, _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionIdInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsSessionNotFoundError()
    {
        // TC-FEE-16: Bãi xe không tìm thấy phiên trả về lỗi SessionNotFound
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound)
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_AccessDenied_ReturnsSessionAccessDeniedError()
    {
        // TC-FEE-17: Khách hàng không sở hữu phiên trả về lỗi SessionAccessDenied
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionAccessDenied)
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), Guid.NewGuid(), "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Completed")]
    public async Task Handle_SessionNotActive_ReturnsSessionNotActiveError(string status)
    {
        // TC-FEE-22, TC-FEE-23: Phiên đã trả tiền hoặc đã hoàn thành trả về lỗi SessionNotActive
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(CreateDefaultContext(status: status))
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionNotActive.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_ParkingServiceUnavailable_ReturnsServiceUnavailableError()
    {
        // TC-FEE-25: Dịch vụ bãi xe mất kết nối trả về ParkingServiceUnavailable
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable)
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_ValidRequest_CalculatesFeeAndDeductsPrepayment()
    {
        // TC-FEE-07: Đỗ 135 phút, phí 40.000, cọc 20.000 -> số tiền còn lại 20.000
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(
                CreateDefaultContext(bookingId: _bookingId, accountId: _accountId))
        };
        var reader = new FakePrepaymentReader { AmountToReturn = 20000 };
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(_validSessionId, response.SessionId);
        Assert.Equal("51A-12345", response.PlateNumber);
        Assert.Equal(135, response.DurationMinutes);
        Assert.Equal(40000, response.TotalFee);
        Assert.Equal(20000, response.DepositAmount);
        Assert.Equal(20000, response.AmountDue);
        Assert.True(reader.WasCalled);
    }

    [Fact]
    public async Task Handle_PrepaymentGreaterThanTotalFee_AmountDueIsZero()
    {
        // TC-FEE-08: Tiền cọc lớn hơn tổng phí -> số tiền còn lại bằng 0
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(
                CreateDefaultContext(bookingId: _bookingId, accountId: _accountId))
        };
        var reader = new FakePrepaymentReader { AmountToReturn = 50000 };
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(40000, response.TotalFee);
        Assert.Equal(50000, response.DepositAmount);
        Assert.Equal(0, response.AmountDue);
    }

    [Fact]
    public async Task Handle_PrepaymentEqualsTotalFee_AmountDueIsZero()
    {
        // TC-FEE-09: Tiền cọc bằng đúng tổng phí -> số tiền còn lại bằng 0
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(
                CreateDefaultContext(bookingId: _bookingId, accountId: _accountId))
        };
        var reader = new FakePrepaymentReader { AmountToReturn = 40000 };
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.AmountDue);
    }

    [Fact]
    public async Task Handle_WalkInVehicleWithoutBooking_DoesNotCallPrepaymentReader()
    {
        // TC-FEE-10: Xe vãng lai không có bookingId -> tiền cọc bằng 0, không gọi reader
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(
                CreateDefaultContext(bookingId: null, accountId: null))
        };
        var reader = new FakePrepaymentReader { AmountToReturn = 20000 };
        var clock = new FakeClock(_calcTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), null, "Staff");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(0, response.DepositAmount);
        Assert.Equal(response.TotalFee, response.AmountDue);
        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task Handle_UsesClockFromInjectedProvider()
    {
        // TC-FEE-12: Giờ tính tiền lấy từ IClock được tiêm vào
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(CreateDefaultContext())
        };
        var reader = new FakePrepaymentReader();
        var customClockTime = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(customClockTime);
        var handler = new GetParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetParkingSessionFeeQuery(_validSessionId.ToString(), _accountId, "Customer");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(customClockTime, result.Value!.CalculatedAtUtc);
    }
}
