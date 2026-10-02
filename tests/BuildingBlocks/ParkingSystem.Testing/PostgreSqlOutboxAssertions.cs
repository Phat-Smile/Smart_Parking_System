using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ParkingSystem.Testing;

public static class PostgreSqlOutboxAssertions
{
    private static readonly string[] AllowedServiceSchemas = ["identity", "parking", "booking", "payment", "notification"];

    public static async Task AssertServiceIsolationAndOutboxMetadataAsync(
        DbContext dbContext,
        string serviceSchema,
        CancellationToken cancellationToken = default)
    {
        Require(AllowedServiceSchemas.Contains(serviceSchema), $"Unexpected service schema '{serviceSchema}'.");

        var serviceSchemas = await QueryStringsAsync(
            dbContext,
            "SELECT schema_name FROM information_schema.schemata " +
            "WHERE schema_name NOT IN ('information_schema', 'pg_catalog', 'public') " +
            "AND schema_name NOT LIKE 'pg_toast%' AND schema_name NOT LIKE 'pg_temp_%' " +
            "ORDER BY schema_name",
            cancellationToken);
        Require(serviceSchemas.SequenceEqual([serviceSchema]), $"Expected only the '{serviceSchema}' service schema, found: {string.Join(", ", serviceSchemas)}.");

        var serviceTables = await QueryStringsAsync(
            dbContext,
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = @service_schema AND table_type = 'BASE TABLE' ORDER BY table_name",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(serviceTables.SequenceEqual(["__EFMigrationsHistory", "inbox_messages", "outbox_messages"]), $"Unexpected tables in '{serviceSchema}': {string.Join(", ", serviceTables)}.");

        var publicTableCount = await QueryScalarAsync<long>(
            dbContext,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'",
            cancellationToken);
        Require(publicTableCount == 0, "The test database must not contain tables in public.");
        Require(await QueryScalarAsync<bool>(
            dbContext,
            "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL",
            cancellationToken), "The test database must not contain public.__EFMigrationsHistory.");
        Require(await QueryScalarAsync<bool>(
            dbContext,
            "SELECT to_regclass('public.outbox_messages') IS NULL",
            cancellationToken), "The test database must not contain public.outbox_messages.");
        Require(await QueryScalarAsync<bool>(
            dbContext,
            "SELECT to_regclass('public.inbox_messages') IS NULL",
            cancellationToken), "The test database must not contain public.inbox_messages.");

        var migrationIds = await QueryStringsAsync(
            dbContext,
            $"SELECT \"MigrationId\" FROM \"{serviceSchema}\".\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            cancellationToken);
        Require(migrationIds.Count == 3 && migrationIds[0].EndsWith("_AddOutboxFoundation", StringComparison.Ordinal) &&
                migrationIds[1].EndsWith("_AddOutboxDispatchLeasing", StringComparison.Ordinal) &&
                migrationIds[2].EndsWith("_AddInboxFoundation", StringComparison.Ordinal),
            $"Unexpected migration history for '{serviceSchema}': {string.Join(", ", migrationIds)}.");

        var inboxColumnCount = await QueryScalarAsync<long>(
            dbContext,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = @service_schema " +
            "AND table_name = 'inbox_messages' AND column_name IN ('message_id', 'consumer_key', 'received_at_utc', 'processed_at_utc') " +
            "AND is_nullable = 'NO'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(inboxColumnCount == 4, $"Expected all four required Inbox columns in '{serviceSchema}'.");

        var inboxConsumerKeyLength = await QueryScalarAsync<int>(
            dbContext,
            "SELECT character_maximum_length FROM information_schema.columns WHERE table_schema = @service_schema " +
            "AND table_name = 'inbox_messages' AND column_name = 'consumer_key'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(inboxConsumerKeyLength == 512, $"Expected consumer_key length 512 in '{serviceSchema}'.");

        var inboxPrimaryKey = await QueryScalarAsync<string>(
            dbContext,
            "SELECT pg_get_constraintdef(constraint_row.oid) FROM pg_constraint AS constraint_row " +
            "JOIN pg_class AS table_row ON table_row.oid = constraint_row.conrelid " +
            "JOIN pg_namespace AS schema_row ON schema_row.oid = table_row.relnamespace " +
            "WHERE schema_row.nspname = @service_schema AND table_row.relname = 'inbox_messages' AND constraint_row.contype = 'p'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(inboxPrimaryKey.Contains("PRIMARY KEY (message_id, consumer_key)", StringComparison.OrdinalIgnoreCase),
            $"Expected composite Inbox primary key in '{serviceSchema}', found '{inboxPrimaryKey}'.");

        var payloadType = await QueryScalarAsync<string>(
            dbContext,
            "SELECT data_type FROM information_schema.columns " +
            "WHERE table_schema = @service_schema AND table_name = 'outbox_messages' AND column_name = 'payload'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(payloadType == "jsonb", $"Expected {serviceSchema}.outbox_messages.payload to be jsonb, found '{payloadType}'.");

        var requiredColumns = await QueryScalarAsync<long>(
            dbContext,
            "SELECT count(*) FROM information_schema.columns " +
            "WHERE table_schema = @service_schema AND table_name = 'outbox_messages' " +
            "AND column_name IN ('event_type', 'payload') AND is_nullable = 'NO'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(requiredColumns == 2, $"Expected event_type and payload to be NOT NULL in '{serviceSchema}'.");

        var leaseColumns = await QueryScalarAsync<long>(
            dbContext,
            "SELECT count(*) FROM information_schema.columns " +
            "WHERE table_schema = @service_schema AND table_name = 'outbox_messages' " +
            "AND column_name IN ('lock_id', 'locked_until_utc', 'next_attempt_at_utc') " +
            "AND is_nullable = 'YES'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(leaseColumns == 3, $"Expected all three nullable dispatch lease fields in '{serviceSchema}'.");

        var pendingIndex = await QueryScalarAsync<string>(
            dbContext,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = @service_schema " +
            "AND indexname = 'ix_outbox_messages_pending'",
            cancellationToken,
            ("service_schema", serviceSchema));
        Require(pendingIndex.Contains("occurred_at_utc", StringComparison.OrdinalIgnoreCase), "The pending Outbox index must include occurred_at_utc.");
        Require(pendingIndex.Contains("processed_at_utc IS NULL", StringComparison.OrdinalIgnoreCase), "The pending Outbox index must filter processed_at_utc IS NULL.");
    }

    private static async Task<IReadOnlyList<string>> QueryStringsAsync(
        DbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = CreateCommand(connection, sql, parameters);
            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                values.Add(reader.GetString(0));
            return values;
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<T> QueryScalarAsync<T>(
        DbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = CreateCommand(connection, sql, parameters);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Require(value is not null and not DBNull, $"Query returned no value: {sql}");
            return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        string sql,
        IEnumerable<(string Name, object Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
