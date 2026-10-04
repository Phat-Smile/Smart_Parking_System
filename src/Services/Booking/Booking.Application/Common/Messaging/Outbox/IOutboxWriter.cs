namespace ParkingSystem.Booking.Application.Common.Messaging.Outbox;

/// <summary>
/// The only way business code publishes an integration event. The row is added to the current
/// DbContext and is committed by the same SaveChanges/transaction as the business state change;
/// the Outbox dispatcher publishes it afterwards.
/// </summary>
public interface IOutboxWriter
{
    /// <param name="eventType">Registered identifier, e.g. <c>booking.&lt;event-name&gt;.v1</c>; must match AddIntegrationEvent.</param>
    void Enqueue<TEvent>(
        TEvent integrationEvent,
        string eventType,
        int eventVersion,
        Guid? correlationId = null,
        Guid? causationId = null)
        where TEvent : class;
}
