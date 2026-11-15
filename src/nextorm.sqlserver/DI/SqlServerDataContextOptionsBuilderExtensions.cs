using System.Data.Common;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// Extension methods that configure a <c>DataContextBuilder</c> to use Microsoft SQL Server.
/// </summary>
public static class SqlServerDataContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the builder to create contexts from the builder alone. The builder must configure routing
    /// and name a default topology; its host supplies the connection, so no connection string is given here.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static DataContextBuilder UseSqlServer(this DataContextBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Factory = (b) => new SqlServerDataContext(b);

        return builder;
    }

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
    /// Configures the builder to create SQL Server contexts with an explicitly declared server version
    /// and approximate vector-search grammar.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="serverVersion">The SQL Server version declared by the caller; it is not probed.</param>
    /// <param name="vectorSearchSyntax">The exact <c>VECTOR_SEARCH</c> grammar to emit.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    /// <remarks>The grammar is explicitly selected and is not inferred from the server major version. Vector search still requires a declared major version of 17 or later.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="serverVersion"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vectorSearchSyntax"/> is undefined.</exception>
    public static DataContextBuilder UseSqlServer(
        this DataContextBuilder builder,
        string connectionString,
        Version serverVersion,
        SqlServerVectorSearchSyntax vectorSearchSyntax)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(serverVersion);
        ValidateVectorSearchSyntax(vectorSearchSyntax);

        builder.Factory = vectorSearchSyntax switch
        {
            SqlServerVectorSearchSyntax.TopN => b => new SqlServerTopNDataContext(serverVersion, connectionString, b),
            SqlServerVectorSearchSyntax.WithApproximate => b => new SqlServerWithApproximateDataContext(serverVersion, connectionString, b),
            _ => throw new ArgumentOutOfRangeException(nameof(vectorSearchSyntax), vectorSearchSyntax, "Undefined SqlServerVectorSearchSyntax value.")
        };

        return builder;
    }

    /// <summary>
    /// Configures the builder to create SQL Server contexts over the caller-supplied connection with an
    /// explicitly declared server version and approximate vector-search grammar.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connection">An already-created connection owned by the caller.</param>
    /// <param name="serverVersion">The SQL Server version declared by the caller; it is not probed.</param>
    /// <param name="vectorSearchSyntax">The exact <c>VECTOR_SEARCH</c> grammar to emit.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    /// <remarks>The grammar is explicitly selected and is not inferred from the server major version. Vector search still requires a declared major version of 17 or later.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="connection"/> or <paramref name="serverVersion"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vectorSearchSyntax"/> is undefined.</exception>
    public static DataContextBuilder UseSqlServer(
        this DataContextBuilder builder,
        DbConnection connection,
        Version serverVersion,
        SqlServerVectorSearchSyntax vectorSearchSyntax)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(serverVersion);
        ValidateVectorSearchSyntax(vectorSearchSyntax);

        builder.Factory = vectorSearchSyntax switch
        {
            SqlServerVectorSearchSyntax.TopN => b => new SqlServerTopNDataContext(serverVersion, connection, b),
            SqlServerVectorSearchSyntax.WithApproximate => b => new SqlServerWithApproximateDataContext(serverVersion, connection, b),
            _ => throw new ArgumentOutOfRangeException(nameof(vectorSearchSyntax), vectorSearchSyntax, "Undefined SqlServerVectorSearchSyntax value.")
        };

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

    private static void ValidateVectorSearchSyntax(SqlServerVectorSearchSyntax vectorSearchSyntax)
    {
        if (!Enum.IsDefined(vectorSearchSyntax))
        {
            throw new ArgumentOutOfRangeException(
                nameof(vectorSearchSyntax), vectorSearchSyntax, "Undefined SqlServerVectorSearchSyntax value.");
        }
    }

    private sealed class SqlServerTopNDataContext : SqlServerDataContext
    {
        public SqlServerTopNDataContext(Version serverVersion, string connectionString, DataContextBuilder optionsBuilder)
            : base(serverVersion, connectionString, optionsBuilder, SqlServerVectorSearchSyntax.TopN)
        {
        }

        public SqlServerTopNDataContext(Version serverVersion, DbConnection connection, DataContextBuilder optionsBuilder)
            : base(serverVersion, connection, optionsBuilder, SqlServerVectorSearchSyntax.TopN)
        {
        }
    }

    private sealed class SqlServerWithApproximateDataContext : SqlServerDataContext
    {
        public SqlServerWithApproximateDataContext(Version serverVersion, string connectionString, DataContextBuilder optionsBuilder)
            : base(serverVersion, connectionString, optionsBuilder, SqlServerVectorSearchSyntax.WithApproximate)
        {
        }

        public SqlServerWithApproximateDataContext(Version serverVersion, DbConnection connection, DataContextBuilder optionsBuilder)
            : base(serverVersion, connection, optionsBuilder, SqlServerVectorSearchSyntax.WithApproximate)
        {
        }
    }
}
