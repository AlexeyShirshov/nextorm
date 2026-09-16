using nextorm.core;

namespace nextorm.postgres.tests;

/// <summary>
/// Builds a <see cref="PostgresDbContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough. Tests that talk to a real server live
/// in nextorm.integration.tests, which starts a PostgreSQL Testcontainers instance (or uses the
/// server named by NEXTORM_POSTGRES_CONNECTION).
/// </summary>
internal static class PostgresTestContext
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new PostgresDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static PostgresDbContext CreatePostgres() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
