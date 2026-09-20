using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Builds a <see cref="PostgresDataContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough. Tests that talk to a real server live
/// in NextORM.Integration.Tests, which starts a PostgreSQL Testcontainers instance (or uses the
/// server named by NEXTORM_POSTGRES_CONNECTION).
/// </summary>
internal static class PostgresTestContext
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static PostgresDataContext CreatePostgres() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
