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

    public static IDataContext CreateQuoted() =>
        new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder().UseQuotedIdentifiers());

    public static IDataContext CreateUppercase() =>
        new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder().UseKeywordCase());

    public static IDataContext CreateMultiline() =>
        new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder().UseMultilineBatchSql());

    public static IDataContext CreateSnakeCaseQuoted() =>
        new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder().UseNamingConvention(SnakeCaseNamingConvention.Instance).UseQuotedIdentifiers());

    public static PostgresDataContext CreatePostgres() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
