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

    /// <summary>
    /// Creates a version-aware PostgreSQL context. <typeparamref name="TMarker"/> selects the concrete
    /// context type: the server version is immutable per concrete context type (the plan cache is keyed
    /// by the context type), so every distinct version needs a distinct marker type. Reusing one marker
    /// with two different versions throws <see cref="InvalidOperationException"/>, which is the
    /// production guard under test.
    /// </summary>
    /// <typeparam name="TMarker">A distinct marker type per versioned context.</typeparam>
    /// <param name="serverVersion">The PostgreSQL server version, or <see langword="null"/> for unset.</param>
    /// <returns>A version-aware PostgreSQL context.</returns>
    public static IDataContext CreateVersioned<TMarker>(Version? serverVersion) =>
        new VersionedPostgresContext<TMarker>(PlaceholderConnectionString, new DataContextBuilder(), serverVersion);

    private sealed class VersionedPostgresContext<TMarker> : PostgresDataContext
    {
        public VersionedPostgresContext(string connectionString, DataContextBuilder optionsBuilder, Version? serverVersion)
            : base(connectionString, optionsBuilder, serverVersion)
        {
        }
    }
}
