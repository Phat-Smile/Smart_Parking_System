using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Messaging.Consumers;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class SlotStatusChangedConsumerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public Result<ReservationDebitState> RefundResultToReturn { get; set; } = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose = PrepaymentPurpose.Deposit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            return Task.FromResult(RefundResultToReturn);
        }
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class =>
            Events.Add(integrationEvent);
    }

    private sealed class InMemoryReservationRepository : IReservationRepository
    {
        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void DiscardPendingChanges() { }
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);
        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);

        public List<Reservation> Items { get; } = [];
        public bool Saved { get; private set; }

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken)
        {
            var query = Items.Where(r => r.UserId == userId);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            return Task.FromResult<IReadOnlyList<Reservation>>(query.ToList());
        }

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken)
        {
            var active = Items.Where(r => r.LotId == lotId &&
                (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList();
            return Task.FromResult<IReadOnlyList<Reservation>>(active);
        }

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
            Guid lotId,
            string plateNumber,
            DateTimeOffset arrivalTime,
            CancellationToken cancellationToken)
        {
            var normalized = plateNumber.Trim().ToUpperInvariant();
            var res = Items.FirstOrDefault(r =>
                r.LotId == lotId &&
                r.PlateNumber.Trim().ToUpperInvariant() == normalized &&
                r.Status == ReservationStatus.Confirmed &&
                arrivalTime >= r.WindowStartAt.AddMinutes(-15) &&
                arrivalTime <= r.AppointmentTime.AddMinutes(Reservation.NoShowGracePeriodMinutes));
            return Task.FromResult(res);
        }

        public void Add(Reservation reservation) => Items.Add(reservation);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private static Reservation CreateHold(Guid lotId, decimal prepaidAmount = 0)
    {
        var reservation = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "59X1-12345",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            FixedNow.AddHours(1),
            prepaidAmount,
            FixedNow);

        return reservation;
    }

    [Fact]
    public async Task WhenSlotEntersMaintenance_CancelsTemporaryHoldWithNoSuitableSlot()
    {
        var lotId = Guid.NewGuid();
        var repo = new InMemoryReservationRepository();
        var hold = CreateHold(lotId);
        repo.Add(hold);

        var handler = new HandleSlotStatusChangedCommandHandler(
            repo,
            new FakeReservationPaymentClient(),
            new FakeClock(FixedNow),
            NullLogger<HandleSlotStatusChangedCommandHandler>.Instance,
            new RecordingOutbox());

        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.Cancelled, hold.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, hold.CancelReason);
        Assert.True(repo.Saved);
    }

    [Fact]
    public async Task WhenSlotEntersUnavailable_CancelsConfirmedReservationAndRefunds()
    {
        var lotId = Guid.NewGuid();
        var repo = new InMemoryReservationRepository();
        var reservation = CreateHold(lotId, prepaidAmount: 50000m);
        reservation.Confirm(FixedNow.AddMinutes(5));
        repo.Add(reservation);

        var handler = new HandleSlotStatusChangedCommandHandler(
            repo,
            new FakeReservationPaymentClient(),
            new FakeClock(FixedNow),
            NullLogger<HandleSlotStatusChangedCommandHandler>.Instance,
            new RecordingOutbox());

        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "B-02",
            "Unavailable",
            "Occupied",
            "AdminOverride",
            FixedNow,
            2);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, reservation.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);
        Assert.True(repo.Saved);
    }

    [Fact]
    public async Task WhenSlotBecomesAvailable_DoesNotCancelAnyReservations()
    {
        var lotId = Guid.NewGuid();
        var repo = new InMemoryReservationRepository();
        var hold = CreateHold(lotId);
        repo.Add(hold);

        var handler = new HandleSlotStatusChangedCommandHandler(
            repo,
            new FakeReservationPaymentClient(),
            new FakeClock(FixedNow),
            NullLogger<HandleSlotStatusChangedCommandHandler>.Instance,
            new RecordingOutbox());

        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Available",
            "Maintenance",
            "Sensor",
            FixedNow,
            3);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.TemporaryHold, hold.Status);
        Assert.False(repo.Saved);
    }

    [Fact]
    public async Task WhenLotHasNoActiveReservations_ReturnsZeroCancelled()
    {
        var lotId = Guid.NewGuid();
        var repo = new InMemoryReservationRepository();

        var handler = new HandleSlotStatusChangedCommandHandler(
            repo,
            new FakeReservationPaymentClient(),
            new FakeClock(FixedNow),
            NullLogger<HandleSlotStatusChangedCommandHandler>.Instance,
            new RecordingOutbox());

        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C-03",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CancelledReservationsCount);
        Assert.False(repo.Saved);
    }

    private sealed class RecordingSender : ISender
    {
        public List<object> SentCommands { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(request);
            var outcome = new SlotStatusChangedOutcomeDto(Guid.NewGuid(), Guid.NewGuid(), "A-01", "Maintenance", 1, "OK");
            return Task.FromResult((TResponse)(object)Result<SlotStatusChangedOutcomeDto>.Success(outcome));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            SentCommands.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(request);
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task SlotStatusChangedConsumer_DispatchesCommandViaSender()
    {
        var sender = new RecordingSender();
        await using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<ISender>(sender)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<SlotStatusChangedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var evt = new SlotStatusChangedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-05",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        await harness.Bus.Publish(evt);

        Assert.True(await harness.GetConsumerHarness<SlotStatusChangedConsumer>()
            .Consumed.Any<SlotStatusChangedIntegrationEvent>(TestContext.Current.CancellationToken));

        Assert.Single(sender.SentCommands);
        var command = Assert.IsType<HandleSlotStatusChangedCommand>(sender.SentCommands[0]);
        Assert.Equal(evt.LotId, command.LotId);
        Assert.Equal(evt.SlotCode, command.SlotCode);
        Assert.Equal(evt.EffectiveStatus, command.EffectiveStatus);
    }
}
