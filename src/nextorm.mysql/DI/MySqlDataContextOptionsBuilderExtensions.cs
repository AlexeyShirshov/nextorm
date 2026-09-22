using System.Data.Common;
using NextORM.Core;

namespace NextORM.MySql;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use MySQL.
/// </summary>
public static class MySqlDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create MySQL contexts that own a connection built from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseMySql(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MySqlDataContext(connectionString, b);

        return builder;
    }
    /// <summary>
    /// Configures the builder to create MySQL contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseMySql(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MySqlDataContext(connection, b);

        return builder;
    }
}
