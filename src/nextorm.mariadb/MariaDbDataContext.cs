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
    /// <summary>
    /// Creates a MariaDB context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>MySqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MariaDbDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a MariaDB context over a caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public MariaDbDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(connection, optionsBuilder)
    {
    }

    /// <summary>The MariaDB SQL dialect.</summary>
    public override ISqlDialect Dialect => MariaDbDialect.Instance;
}
