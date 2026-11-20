using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetPublicParkingSessionFee;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Queries;

public sealed class GetPublicParkingSessionFeeQueryHandlerTests
{
    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class FakeParkingSessionGateway : IParkingSessionGateway
    {
        public Result<ParkingSessionFeeContext>? ResultToReturn { get; set; }
        public string? LastQrToken { get; private set; }

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
            Guid sessionId,
            Guid? callerAccountId,
            string callerRole,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextByQrAsync(
            string qrToken,
            CancellationToken cancellationToken = default)
        {
            LastQrToken = qrToken;
            return Task.FromResult(ResultToReturn
                ?? Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound));
        }
    }

    private sealed class FakePrepaymentReader : IPrepaymentReader
    {
        public long AmountToReturn { get; set; }

        public Task<long> GetPrepaidAmountAsync(
            Guid accountId,
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AmountToReturn);
        }
    }

    private readonly DateTimeOffset _entryTime = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _calcTime = new(2026, 11, 20, 9, 30, 0, TimeSpan.Zero);
    private readonly Guid _sessionId = Guid.Parse("3f6c2a52-1d8e-4c55-9d1b-6a7f0c1b2e44");
    private readonly Guid _lotId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private ParkingSessionFeeContext CreateContext(
        string status = "Active",
        string plateNumber = "51F-123.89",
        Guid? bookingId = null,
        Guid? accountId = null)
    {
        return new ParkingSessionFeeContext(
            SessionId: _sessionId,
            ParkingLotId: _lotId,
            PlateNumber: plateNumber,
            VehicleType: "Car",
            Status: status,
            EntryTimeUtc: _entryTime,
            AccountId: accountId,
            BookingId: bookingId,
            Pricing: new PricingSnapshotDto(
                PolicyId: "b1d0c6a4-7f3e-4a0b-8d2c-5e9f1a2b3c4d",
                CalculationMethod: "Block",
                BlockMinutes: 30,
                PricePerUnit: 10000));
    }

    [Fact]
    public async Task Handle_ValidActiveSession_ReturnsOkWithMaskedPlateAndNoPaymentUrl()
    {
        // 90 minutes parked with 30-min block at 10,000 VND = 3 blocks = 30,000 VND
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(CreateContext(status: "Active", plateNumber: "51F-123.89"))
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var query = new GetPublicParkingSessionFeeQuery("valid-qr-token-123");
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(_sessionId, response.SessionId);
        Assert.Equal("Active", response.Status);
        Assert.Equal(_entryTime, response.EntryTimeUtc);
        Assert.Equal(_calcTime, response.CalculatedAtUtc);
        Assert.Equal("Block", response.CalculationMethod);
        Assert.Equal(30000, response.TotalFee);
        Assert.Equal(0, response.PaidAmount);
        Assert.Equal(30000, response.AmountDue);

        // Security / PII: Masked plate conforms to NFR-15
        Assert.Equal("51F-***.89", response.MaskedPlateNumber);

        // Security: Ensure response DTO does not contain paymentUrl (Condition 4 Tin 200)
        var responseProperties = typeof(PublicParkingSessionFeeResponse).GetProperties();
        Assert.DoesNotContain(responseProperties, p => p.Name.Equals("PaymentUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_BookingPrepayment_DeductsDepositFromAmountDue()
    {
        var accountId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(
                CreateContext(status: "Active", bookingId: bookingId, accountId: accountId))
        };
        var reader = new FakePrepaymentReader { AmountToReturn = 15000 };
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var result = await handler.Handle(new GetPublicParkingSessionFeeQuery("qr-with-booking"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(30000, result.Value!.TotalFee);
        Assert.Equal(15000, result.Value.PaidAmount);
        Assert.Equal(15000, result.Value.AmountDue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceQr_ReturnsUniformSessionNotFound404(string emptyQr)
    {
        var gateway = new FakeParkingSessionGateway();
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var result = await handler.Handle(new GetPublicParkingSessionFeeQuery(emptyQr), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_SessionNotFoundInParking_ReturnsUniformSessionNotFound404()
    {
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound)
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var result = await handler.Handle(new GetPublicParkingSessionFeeQuery("non-existent-qr"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionNotFound.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Paid")]
    public async Task Handle_SessionNotActive_ReturnsUniformSessionNotFound404(string inactiveStatus)
    {
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Success(CreateContext(status: inactiveStatus))
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var result = await handler.Handle(new GetPublicParkingSessionFeeQuery("closed-session-qr"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.SessionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_ParkingServiceUnavailable_PropagatesServiceUnavailable()
    {
        var gateway = new FakeParkingSessionGateway
        {
            ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable)
        };
        var reader = new FakePrepaymentReader();
        var clock = new FakeClock(_calcTime);
        var handler = new GetPublicParkingSessionFeeQueryHandler(gateway, reader, clock);

        var result = await handler.Handle(new GetPublicParkingSessionFeeQuery("valid-qr"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ParkingFeeErrors.ParkingServiceUnavailable.Code, result.Error!.Code);
    }
}
