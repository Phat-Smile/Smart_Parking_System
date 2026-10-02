# Messaging conventions

## Transactional Outbox

Each business service owns its Outbox table in its schema. A future business transaction will write its state changes and serialized event rows to that service's Outbox within one PostgreSQL transaction. The dispatcher runs in the service's Infrastructure layer, claims rows through the transport-neutral Application-owned `IOutboxStore`, and publishes through MassTransit. There is no distributed transaction between PostgreSQL and RabbitMQ.

Claiming runs in a short PostgreSQL transaction. The store selects eligible rows in occurrence order with `FOR UPDATE SKIP LOCKED`, assigns one fresh `lock_id` and a finite `locked_until_utc` lease to the claimed batch, saves, and commits. Publishing starts only after that commit. Other replicas skip rows locked by this transaction. After a crash, a row with an expired lease is eligible again. A dispatcher updates a row only when its `id`, `lock_id`, and unprocessed state still match.

On publish success, the dispatcher records `processed_at_utc`, clears the lease and error, and retains `retry_count`. On failure, it increments `retry_count`, stores a short sanitized reason, sets `next_attempt_at_utc`, and clears the lease. Retry delay doubles from one second and caps at 30 seconds by default. Retries continue; this foundation has no dead-letter state. Empty polls wait one second by default. Options under `Outbox` configure enablement, polling interval, batch size, lease duration, and retry limits.

Delivery is **at-least-once**. If RabbitMQ accepts a publish and the process stops before it records `processed_at_utc`, the lease eventually expires and the event may be published again. This is expected; exactly-once delivery is not claimed.

## Transactional Inbox and consumer idempotency

Each receiving service owns a private `inbox_messages` table in its schema. Inbox identity is the composite `(message_id, consumer_key)`: one logical event can be handled by more than one consumer in the same service, and each consumer uses an explicit stable key such as `booking.payment-completed.v1`. Do not derive the key from a CLR namespace or class name. Reusing a registered key for another consumer, or registering a consumer twice, fails during service configuration.

The Infrastructure consumer filter requires the inbound MassTransit `MessageId` and passes it with the stable consumer key to the Application-owned `IInboxExecutionCoordinator`. Missing or empty `MessageId` fails the delivery; the consumer never generates a replacement ID. The Persistence coordinator uses that service's existing scoped DbContext and one local PostgreSQL transaction for the Inbox claim, consumer/application handler, local persistence changes, any custom Outbox rows, Inbox completion timestamp, and commit. The claim is an `INSERT ... ON CONFLICT (message_id, consumer_key) DO NOTHING` against the composite primary key. PostgreSQL waits for a concurrent conflicting transaction: after a commit the second delivery skips the handler, while after rollback the next delivery may claim and process it.

If the handler fails, the transaction rolls back, including the Inbox claim and all local database writes. The exception propagates through MassTransit; the broker delivery is not acknowledged as successfully processed. On success, the Inbox row and any outgoing custom Outbox rows commit together. Replaying the same `MessageId + ConsumerKey` then skips the handler. This provides idempotent local consumer execution for a stable key and message ID, not global exactly-once messaging. External effects outside the local database transaction need their own idempotency strategy.

Consumers should keep the Inbox transaction limited to local database work and outgoing Outbox rows. Do not perform long HTTP calls, SMTP sends, direct RabbitMQ publishes, or third-party payment calls inside it. Commit local state and an Outbox row atomically, then perform external integration asynchronously.

Inbox rows must remain available for at least the realistic broker redelivery and operator replay window. No Inbox cleanup job is implemented; each service still needs to define and operate its retention policy. Readiness does not depend on Inbox counts, age, or cleanup.

## Event identity and registry

A service's Infrastructure registry explicitly maps `(EventType, EventVersion)` to a CLR contract using `AddIntegrationEvent<TEvent>(eventType, eventVersion)`. Duplicate keys fail when the registry is resolved during host startup. The dispatcher deserializes JSON into that registered type; unknown keys and incompatible payloads remain pending and receive retry state. It never resolves arbitrary assembly-qualified names from database content.

The logical identifier convention for future contracts is:

```text
<service>.<event-name>.v<version>
```

For example, `parking.<event-name>.v1` shows the shape without defining a production event. The owning service controls the event contract. Consumers depend on that public contract and must not depend on another service's internal domain entities.

MassTransit identifies message contracts using the fully qualified CLR contract type and derives RabbitMQ topology from that identity. The dispatcher preserves the Outbox `EventType` and `EventVersion` in transport headers named `event-type` and `event-version`. It sets MassTransit `MessageId` to the Outbox row ID, maps `CorrelationId` to the MassTransit correlation ID, and maps `CausationId` to `InitiatorId`. Metadata is not regenerated during dispatch.

## Versioning

- Published event contracts are immutable.
- A breaking contract change requires a new event version.
- `EventVersion` in an Outbox row must match the registered contract version.
- A consumer should tolerate unknown optional fields and should not infer another service's internal model.

## Phase 05C boundary

The custom Outbox dispatcher and transactional Inbox are implemented for Identity, Parking, Booking, Payment, and Notification. There are currently zero production event mappings, no production business event contracts, and no production consumers or receive endpoints. Test-only contracts and queues exist only in disposable integration tests.

MassTransit EF Outbox/Inbox features are not enabled. Custom service-owned Outbox and Inbox tables are the only persistence mechanisms for these purposes. Domain-event capture and automatic event-to-Outbox conversion will be introduced alongside real business vertical slices.
