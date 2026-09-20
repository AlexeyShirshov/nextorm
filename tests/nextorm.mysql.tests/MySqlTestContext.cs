using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// Builds a <see cref="MySqlDataContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class MySqlTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new MySqlDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static MySqlDataContext CreateMySql() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
