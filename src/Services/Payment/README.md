# Payment

## Responsibility

Wallet and ledger; payment transactions; VNPay integration boundary; deposits, booking prepayment, parking payment, and refunds.

## Projects

- Payment.Domain
- Payment.Application
- Payment.Persistence
- Payment.Infrastructure
- Payment.WebAPI

No business entities or feature endpoints exist yet. Persistence contains only the service-owned Outbox, Inbox, and three EF migrations.
