using NextORM.Core;

namespace NextORM.Sqlite.Tests;

internal static class SqliteTestContext
{
    private const string PlaceholderConnectionString = "Data Source=:memory:";

    public static IDataContext Create() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static IDataContext CreateQuoted() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder().UseQuotedIdentifiers());

    public static IDataContext CreateUppercase() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder().UseKeywordCase());

    public static IDataContext CreateUppercaseQuoted() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder().UseKeywordCase().UseQuotedIdentifiers());

    public static IDataContext CreateSnakeCase() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder().UseNamingConvention(SnakeCaseNamingConvention.Instance));

    public static IDataContext CreateSnakeCaseQuoted() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder().UseNamingConvention(SnakeCaseNamingConvention.Instance).UseQuotedIdentifiers());

    public static SqliteDataContext CreateSqlite() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
