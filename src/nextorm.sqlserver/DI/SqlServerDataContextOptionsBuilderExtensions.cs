using System.Data.Common;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use Microsoft SQL Server.
/// </summary>
public static class SqlServerDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create SQL Server contexts that own a connection built from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseSqlServer(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new SqlServerDataContext(connectionString, b);

        return builder;
    }
    /// <summary>
    /// Configures the builder to create SQL Server contexts over the caller-supplied
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseSqlServer(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqlServerDataContext(connection, b);

        return builder;
    }
}