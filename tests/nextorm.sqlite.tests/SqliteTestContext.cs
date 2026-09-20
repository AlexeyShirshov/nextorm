using NextORM.Core;

namespace NextORM.Sqlite.Tests;

internal static class SqliteTestContext
{
    private const string PlaceholderConnectionString = "Data Source=:memory:";

    public static IDataContext Create() =>
        new SqliteDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static SqliteDataContext CreateSqlite() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
