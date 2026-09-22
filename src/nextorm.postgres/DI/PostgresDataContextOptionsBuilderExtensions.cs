using System.Data.Common;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use PostgreSQL.
/// </summary>
public static class PostgresDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create PostgreSQL contexts that own a connection built from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UsePostgres(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new PostgresDataContext(connectionString, b);

        return builder;
    }
    /// <summary>
    /// Configures the builder to create PostgreSQL contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UsePostgres(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new PostgresDataContext(connection, b);

        return builder;
    }
}
