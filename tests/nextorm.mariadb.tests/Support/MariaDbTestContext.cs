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

    /// <summary>
    /// Creates a version-aware MariaDB context. MariaDB has no per-context-type version guard, so the
    /// same concrete <see cref="MariaDbDataContext"/> type may be used with any version.
    /// </summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    /// <returns>A version-aware MariaDB context.</returns>
    public static IDataContext Create(Version? serverVersion) =>
        new MariaDbDataContext(PlaceholderConnectionString, new DataContextBuilder(), serverVersion);

    /// <summary>Creates a version-aware <see cref="MariaDbDataContext"/> (no type guard applies).</summary>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    /// <returns>A version-aware MariaDB context.</returns>
    public static MariaDbDataContext CreateMariaDb(Version? serverVersion) =>
        new(PlaceholderConnectionString, new DataContextBuilder(), serverVersion);
}
