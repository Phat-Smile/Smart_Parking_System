using MassTransit;

namespace ParkingSystem.Booking.Infrastructure.Messaging.Inbox;

internal sealed class InboxConsumerDefinition<TConsumer>(string consumerKey) : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        consumerConfigurator.UseFilter(new InboxConsumerFilter<TConsumer>(consumerKey));
    }
}
