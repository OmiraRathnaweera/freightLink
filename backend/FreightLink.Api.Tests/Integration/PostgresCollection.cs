using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Forces every test class that spins up its own real Postgres container via
/// <see cref="PostgresWebApplicationFactory"/> (<see cref="DatabaseConstraintTests"/>,
/// <see cref="MigrationTests"/>, <see cref="TransactionRollbackTests"/>) into one xUnit
/// collection, so they run sequentially instead of xUnit's default cross-class parallelism.
/// Starting several Testcontainers-managed Postgres containers concurrently was observed to
/// deadlock in this environment (each class-level <c>IClassFixture</c>/<c>IAsyncLifetime</c>
/// creates its own container, and xUnit runs different test classes in parallel by default) —
/// this collection trades a little wall-clock time for a run that reliably completes.
/// </summary>
[CollectionDefinition(Name)]
public class PostgresCollection
{
    public const string Name = "Postgres";
}
