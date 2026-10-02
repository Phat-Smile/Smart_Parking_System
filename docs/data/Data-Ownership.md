# Data ownership

This document records logical ownership and the technical persistence introduced by Phases 04C, 05B, and 05C. It does not define future business-table schemas.

- Identity owns account, authentication, profile, role, registered-vehicle, OTP, and refresh-session data.
- Parking owns parking-facility/layout, operating-hours/pricing, parking-session, session-QR, occupancy, spot-assignment, and realtime parking state.
- Booking owns hold, scheduled-booking, capacity, cancellation, and no-show data.
- Payment owns wallet, ledger, transaction, deposit, prepayment, parking-payment, refund, and payment-idempotency data.
- Notification owns notification, delivery, email-notification, and reminder data.

SessionQR belongs to ParkingSession and is owned by the Parking Service. It is unique to that parking session and does not belong to User or Vehicle.

Guest does NOT require an Account. Guest is not a database role and has no synthetic account.

Future cross-service relationships use IDs and contracts. They do not use database foreign keys across service ownership boundaries.

## PostgreSQL schema ownership

The local development stack uses one PostgreSQL 17 instance and one database, `parking_system`. Each service owns one schema in that database:

| Service | PostgreSQL schema | Private technical tables |
| --- | --- | --- |
| Identity | `identity` | `identity.outbox_messages`, `identity.inbox_messages` |
| Parking | `parking` | `parking.outbox_messages`, `parking.inbox_messages` |
| Booking | `booking` | `booking.outbox_messages`, `booking.inbox_messages` |
| Payment | `payment` | `payment.outbox_messages`, `payment.inbox_messages` |
| Notification | `notification` | `notification.outbox_messages`, `notification.inbox_messages` |

Each service's `DbContext` sets its matching schema as the model default. A service may read and write only within its own schema. Cross-schema access and joins are prohibited, and no foreign keys may cross service boundaries; relationships between services use IDs and contracts.

The Outbox and Inbox tables are private technical infrastructure owned by the corresponding service. Each service also owns its own `__EFMigrationsHistory` table in the same schema. The custom dispatcher uses nullable `lock_id`, `locked_until_utc`, and `next_attempt_at_utc` columns on that same Outbox table for expiring claim leases and retry scheduling. Inbox rows use the composite primary key `(message_id, consumer_key)` so each logical handler can process a message idempotently. Phase 05C adds no business table or shared schema.
