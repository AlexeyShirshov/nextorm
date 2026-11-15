using System.Data.Common;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use MariaDB.
/// </summary>
public static class MariaDbDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create contexts from the builder alone. The builder must configure routing
    /// and name a default topology; its host supplies the connection, so no connection string is given here.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseMariaDb(this DataContextBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Factory = (b) => new MariaDbDataContext(b);

        return builder;
    }

    /// <summary>
    /// Configures the builder to create MariaDB contexts that own a connection built from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The MariaDB connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseMariaDb(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MariaDbDataContext(connectionString, b);

        return builder;
    }
    /// <summary>
    /// Configures the builder to create MariaDB contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseMariaDb(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MariaDbDataContext(connection, b);

        return builder;
    }
}
