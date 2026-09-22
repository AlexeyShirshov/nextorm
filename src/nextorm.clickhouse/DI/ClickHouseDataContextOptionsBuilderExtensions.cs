using System.Data.Common;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use ClickHouse.
/// </summary>
public static class ClickHouseDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create ClickHouse contexts that own a connection built from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The ClickHouse connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new ClickHouseDataContext(connectionString, b);

        return builder;
    }
    /// <summary>
    /// Configures the builder to create ClickHouse contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new ClickHouseDataContext(connection, b);

        return builder;
    }
}
