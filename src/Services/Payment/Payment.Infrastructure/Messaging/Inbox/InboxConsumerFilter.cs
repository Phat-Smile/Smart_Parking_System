using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Payment.Application.Common.Messaging.Inbox;

namespace ParkingSystem.Payment.Infrastructure.Messaging.Inbox;

internal sealed class InboxConsumerFilter<TConsumer>(string consumerKey) : IFilter<ConsumerConsumeContext<TConsumer>>
    where TConsumer : class, IConsumer
{
    public async Task Send(ConsumerConsumeContext<TConsumer> context, IPipe<ConsumerConsumeContext<TConsumer>> next)
    {
        if (context.MessageId is not { } messageId || messageId == Guid.Empty)
            throw new InvalidOperationException("An inbound integration message must have a non-empty MassTransit MessageId for Inbox idempotency.");

        var serviceProvider = context.GetPayload<IServiceProvider>();
        var coordinator = serviceProvider.GetRequiredService<IInboxExecutionCoordinator>();
        var message = new InboxMessageDescriptor(messageId, consumerKey, DateTimeOffset.UtcNow);
        await coordinator.ExecuteAsync(message, token => next.Send(context), context.CancellationToken);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("inbox-idempotency");
}