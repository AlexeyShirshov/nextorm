using nextorm.core;

namespace nextorm.mysql.tests;

/// <summary>
/// Builds a <see cref="MySqlDbContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class MySqlTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new MySqlDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static MySqlDbContext CreateMySql() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
