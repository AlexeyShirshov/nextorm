using System.Data.Common;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
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

    /// <summary>
    /// Configures the builder to create ClickHouse contexts over the caller-supplied
    /// <paramref name="client"/>. The caller keeps ownership: the context never disposes the client, and
    /// its own connection is built from a copy of the client's settings.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="client">An already-created client owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, ClickHouseClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        builder.Factory = (b) => new ClickHouseDataContext(client, b);

        return builder;
    }

    /// <summary>
    /// Configures the builder to create ClickHouse contexts over the caller-supplied
    /// <paramref name="dataSource"/>. The caller keeps ownership: the context never disposes the data
    /// source or its pooled client, and its own connection is built from a copy of the data source's
    /// settings.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="dataSource">An already-created data source owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dataSource"/> is <see langword="null"/>.</exception>
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, ClickHouseDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        builder.Factory = (b) => new ClickHouseDataContext(dataSource, b);

        return builder;
    }
}
