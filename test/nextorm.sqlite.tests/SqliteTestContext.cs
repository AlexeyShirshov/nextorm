using nextorm.core;

namespace nextorm.sqlite.tests;

internal static class SqliteTestContext
{
    private const string PlaceholderConnectionString = "Data Source=:memory:";

    public static IDataContext Create() =>
        new SqliteDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static SqliteDbContext CreateSqlite() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
