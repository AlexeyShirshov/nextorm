using System.Data.Common;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite;

/// <summary>
/// Data context for SQLite. Wraps the <c>Microsoft.Data.Sqlite</c> driver for connections and
/// parameters, registers the SQLite function set on every connection and renders SQL through
/// <c>SqliteDialect</c>.
/// </summary>
public class SqliteDataContext : DataContext
{
    /// <summary>
    /// Creates a SQLite context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>SqliteConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqliteDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }
    /// <summary>
    /// Creates a SQLite context over a caller-supplied connection, which the context does not dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public SqliteDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }
    /// <summary>Creates a new <c>SqliteConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>A new, unopened SQLite connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqliteConnection(connectionString);

    /// <summary>
    /// Registers the custom SQLite functions on <paramref name="connection"/> so they are available to
    /// generated SQL.
    /// </summary>
    /// <param name="connection">The connection the context has just started using.</param>
    protected override void OnConnectionCreated(DbConnection connection)
    {
        // Registered for supplied connections too, matching the previous behaviour.
        if (connection is SqliteConnection sqliteConnection)
            SQLiteFunctions.Register(sqliteConnection);
    }

    /// <inheritdoc/>
    public override ISqlDialect Dialect => SqliteDialect.Instance;

    /// <summary>
    /// Creates a <c>SqliteParameter</c>, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> because Microsoft.Data.Sqlite rejects an unset parameter value.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new SQLite parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // Microsoft.Data.Sqlite throws "Value must be set." when a parameter holds null,
        // so unset/null values must be passed as DBNull.
        return new SqliteParameter(name, value ?? DBNull.Value);
    }
}
