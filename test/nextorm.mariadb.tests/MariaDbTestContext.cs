using nextorm.core;

namespace nextorm.mariadb.tests;

/// <summary>
/// Builds a <see cref="MariaDbContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class MariaDbTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new MariaDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static MariaDbContext CreateMariaDb() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
