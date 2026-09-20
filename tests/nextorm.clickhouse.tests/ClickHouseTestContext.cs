using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Builds a <see cref="ClickHouseDataContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class ClickHouseTestContext
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    public static IDataContext Create() =>
        new ClickHouseDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static ClickHouseDataContext CreateClickHouse() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
