using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Verifies the full EF Core migration chain (30 migrations under <c>backend/Migrations/</c> at the
/// time of writing) actually applies cleanly to a real Postgres instance. <see cref="CustomWebApplicationFactory"/>
/// deliberately skips <c>Database.Migrate()</c> (InMemory doesn't support it), so this is the only
/// place migrations are ever exercised. Uses a dedicated <see cref="PostgresWebApplicationFactory"/>
/// instance (not shared via <c>IClassFixture</c>) so the container is guaranteed fresh — i.e. zero
/// migrations applied — right before <c>Migrate()</c> runs.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MigrationTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Migrate_AppliesAllMigrations_AndCoreTablesExist()
    {
        // PostgresWebApplicationFactory.InitializeAsync() already ran Database.MigrateAsync() once
        // against this fresh container (the same call Program.cs makes at startup). Calling it again
        // here proves it's safely idempotent (Migrate() only applies migrations not yet recorded in
        // __EFMigrationsHistory) and is the explicit act this test exists to verify.
        await _factory.MigrateAsync();

        await using var db = _factory.CreateDbContext();

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();

        Assert.NotEmpty(appliedMigrations);
        Assert.Empty(pendingMigrations);

        // Spot-check that core tables from across the migration history actually exist post-migrate —
        // proves the chain applied schema changes end-to-end, not just recorded history rows.
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            foreach (var table in new[] { "Users", "Loads", "Assignments", "Trips", "Invoices", "AgentWorkflowRuns" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT to_regclass(@table)::text";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "table";
                parameter.Value = $"\"{table}\"";
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync();
                Assert.True(result is not null and not DBNull, $"Expected table '{table}' to exist after migration.");
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
