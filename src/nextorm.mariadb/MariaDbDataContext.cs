using System.Data.Common;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

/// <summary>
/// Data context for MariaDB. Reuses the <c>MySqlConnector</c> connection and parameter behaviour of
/// <see cref="MySqlDataContext"/> but renders SQL through <c>MariaDbDialect</c>.
/// </summary>
public class MariaDbDataContext : MySqlDataContext
{
    private readonly ISqlDialect _dialect;

    /// <summary>
    /// Creates a MariaDB context that owns a connection built lazily from
    /// <paramref name="connectionString"/>. The context is unversioned: no version-gated form is
    /// emitted.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>MySqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MariaDbDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
        => _dialect = MariaDbDialect.Instance;

    /// <summary>
    /// Creates a MariaDB context over a caller-supplied connection, which the context does not
    /// dispose. The context is unversioned: no version-gated form is emitted.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MariaDbDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(connection, optionsBuilder)
        => _dialect = MariaDbDialect.Instance;

    /// <summary>
    /// Creates a version-aware MariaDB context that owns a connection built lazily from
    /// <paramref name="connectionString"/>. MariaDB 13.0+ exposes <c>UPDATE ... RETURNING</c>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>MySqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbDataContext(string connectionString, DataContextBuilder optionsBuilder, Version? serverVersion)
        : base(connectionString, null, optionsBuilder, serverVersion)
        => _dialect = serverVersion is null ? MariaDbDialect.Instance : new MariaDbDialect(serverVersion);

    /// <summary>
    /// Creates a version-aware MariaDB context over a caller-supplied connection, which the context
    /// does not dispose. MariaDB 13.0+ exposes <c>UPDATE ... RETURNING</c>.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    /// <param name="serverVersion">The MariaDB server version, or <see langword="null"/> for unset.</param>
    public MariaDbDataContext(DbConnection connection, DataContextBuilder optionsBuilder, Version? serverVersion)
        : base(null, connection, optionsBuilder, serverVersion)
        => _dialect = serverVersion is null ? MariaDbDialect.Instance : new MariaDbDialect(serverVersion);

    /// <summary>The MariaDB SQL dialect (version-aware when a server version was supplied).</summary>
    public override ISqlDialect Dialect => _dialect;
}
