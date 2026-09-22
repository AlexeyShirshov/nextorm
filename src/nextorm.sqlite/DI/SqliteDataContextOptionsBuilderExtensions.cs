using System.Data.Common;
using NextORM.Core;

namespace NextORM.Sqlite;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use SQLite.
/// </summary>
public static class SqliteDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create SQLite contexts over the database file at
    /// <paramref name="filepath"/> (opened with <c>Data Source='{filepath}'</c>).
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="filepath">Path to the SQLite database file.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseSqlite(this DataContextBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

#if DEBUG
        if (!File.Exists(filepath))
            throw new ArgumentException($"File '{filepath}' does not exist");
#endif

        builder.Factory = (b) => new SqliteDataContext($"Data Source='{filepath}'", b);
        return builder;
    }
    /// <summary>
    /// Configures the builder to create SQLite contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseSqlite(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqliteDataContext(connection, b);

        return builder;
    }
}