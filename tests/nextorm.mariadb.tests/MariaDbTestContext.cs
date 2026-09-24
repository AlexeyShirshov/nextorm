using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// Builds a <see cref="MariaDbDataContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough.
/// </summary>
internal static class MariaDbTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    public static IDataContext Create() =>
        new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static IDataContext CreateQuoted() =>
        new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder().UseQuotedIdentifiers());

    public static IDataContext CreateUppercase() =>
        new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder().UseUppercaseKeywords());

    public static MariaDbDataContext CreateMariaDb() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}
