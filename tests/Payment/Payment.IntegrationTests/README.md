# Payment IntegrationTests

Runs Payment Persistence migrations and Outbox assertions against an isolated PostgreSQL 17 Testcontainers instance. Tests use the connection string supplied by Testcontainers and never touch the local Compose database.
