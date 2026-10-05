# Identity

## Responsibility

Accounts, email/OTP and Google authentication, profiles, roles, and registered vehicles. Guest users have no account and are not a database role.

## Projects

- Identity.Domain
- Identity.Application
- Identity.Persistence
- Identity.Infrastructure
- Identity.WebAPI

No business entities or feature endpoints exist yet. Persistence contains only the service-owned Outbox, Inbox, and three EF migrations.
