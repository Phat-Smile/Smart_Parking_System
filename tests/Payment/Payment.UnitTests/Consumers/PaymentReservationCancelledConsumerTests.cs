using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RecordPrepaymentForfeited;
using ParkingSystem.Payment.Infrastructure.Messaging.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Consumers;

public sealed class PaymentReservationCancelledConsumerTests
{
    private sealed class RecordingSender : ISender
    {
        public List<object> SentCommands { get; } = [];
        public Func<object, object>? OnSend { get; set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(request);
            if (OnSend is not null)
            {
                return Task.FromResult((TResponse)OnSend(request));
            }

            return Task.FromResult((TResponse)(object)Result<Guid>.Success(Guid.NewGuid()));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            SentCommands.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(request);
            return Task.FromResult<object?>(OnSend?.Invoke(request));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static async Task<ServiceProvider> BuildHarnessProviderAsync(RecordingSender sender)
    {
        var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<ISender>(sender)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<PaymentReservationCancelledConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return provider;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public async Task Consume_WhenForfeitedAmountZeroOrNegative_DoesNotSendCommand(decimal forfeitedAmount)
    {
        var sender = new RecordingSender();
        await using var provider = await BuildHarnessProviderAsync(sender);
        var harness = provider.GetRequiredService<ITestHarness>();

        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CustomerCancelled",
            refundAmount: 20_000m,
            DateTimeOffset.UtcNow,
            forfeitedAmount);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        var consumerHarness = harness.GetConsumerHarness<PaymentReservationCancelledConsumer>();
        Assert.True(await consumerHarness.Consumed.Any<ReservationCancelledIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Empty(sender.SentCommands);
    }

    [Fact]
    public async Task Consume_WhenForfeitedAmountPositive_DispatchesCommandAndCompletesWithoutFault()
    {
        var sender = new RecordingSender();
        var expectedEntryId = Guid.NewGuid();
        sender.OnSend = _ => Result<Guid>.Success(expectedEntryId);

        await using var provider = await BuildHarnessProviderAsync(sender);
        var harness = provider.GetRequiredService<ITestHarness>();

        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "NoShow",
            refundAmount: 0m,
            new DateTimeOffset(2026, 11, 20, 1, 15, 0, TimeSpan.Zero),
            forfeitedAmount: 60_000m);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        var consumerHarness = harness.GetConsumerHarness<PaymentReservationCancelledConsumer>();
        var consumed = await consumerHarness.Consumed
            .SelectAsync<ReservationCancelledIntegrationEvent>(
                x => x.Context.Message.ReservationId == evt.ReservationId,
                TestContext.Current.CancellationToken)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(consumed);
        Assert.Null(consumed.Exception);
        var command = Assert.IsType<RecordPrepaymentForfeitedCommand>(Assert.Single(sender.SentCommands));
        Assert.Equal(evt.ReservationId, command.ReservationId);
        Assert.Equal(evt.UserId, command.UserId);
        Assert.Equal(evt.LotId, command.LotId);
        Assert.Equal(evt.ForfeitedAmount, command.ForfeitedAmount);
        Assert.Equal(evt.CancelReason, command.CancelReason);
        Assert.Equal(evt.CancelledAtUtc, command.CancelledAtUtc);
    }

    [Theory]
    [InlineData("payment.wallet_not_found")]
    [InlineData("payment.reservation_not_debited")]
    [InlineData("payment.prepayment_already_settled")]
    [InlineData("payment.forfeit_amount_exceeds_deposit")]
    public async Task Consume_WhenPermanentErrorReturned_AcknowledgesWithoutFault(string errorCode)
    {
        var sender = new RecordingSender
        {
            OnSend = _ => Result<Guid>.Failure(new Error(errorCode, "Permanent test error"))
        };

        await using var provider = await BuildHarnessProviderAsync(sender);
        var harness = provider.GetRequiredService<ITestHarness>();

        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "NoShow",
            0m,
            DateTimeOffset.UtcNow,
            20_000m);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        var consumerHarness = harness.GetConsumerHarness<PaymentReservationCancelledConsumer>();
        var consumed = await consumerHarness.Consumed
            .SelectAsync<ReservationCancelledIntegrationEvent>(
                x => x.Context.Message.ReservationId == evt.ReservationId,
                TestContext.Current.CancellationToken)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(consumed);
        Assert.Null(consumed.Exception);
        Assert.False(await harness.Published.Any<Fault<ReservationCancelledIntegrationEvent>>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Consume_WhenRetryableErrorReturned_ThrowsInvalidOperationExceptionToTriggerRetry()
    {
        var sender = new RecordingSender
        {
            OnSend = _ => Result<Guid>.Failure(ReservationDepositErrors.WalletBusy)
        };

        await using var provider = await BuildHarnessProviderAsync(sender);
        var harness = provider.GetRequiredService<ITestHarness>();

        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "NoShow",
            0m,
            DateTimeOffset.UtcNow,
            20_000m);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        var consumerHarness = harness.GetConsumerHarness<PaymentReservationCancelledConsumer>();
        var consumed = await consumerHarness.Consumed
            .SelectAsync<ReservationCancelledIntegrationEvent>(
                x => x.Context.Message.ReservationId == evt.ReservationId,
                TestContext.Current.CancellationToken)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(consumed);
        var ex = Assert.IsType<InvalidOperationException>(consumed.Exception);
        Assert.Contains(ReservationDepositErrors.WalletBusy.Code, ex.Message);
    }
}
