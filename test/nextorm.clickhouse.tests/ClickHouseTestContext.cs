using nextorm.core;

namespace nextorm.clickhouse.tests;

/// <summary>
/// Builds a <see cref="ClickHouseDbContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class ClickHouseTestContext
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    public static IDataContext Create() =>
        new ClickHouseDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static ClickHouseDbContext CreateClickHouse() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
