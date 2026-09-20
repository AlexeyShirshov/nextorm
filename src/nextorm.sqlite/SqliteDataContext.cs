using System.Data.Common;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite;

public class SqliteDataContext : DataContext
{
    public SqliteDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }
    public SqliteDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new SqliteConnection(connectionString);

    protected override void OnConnectionCreated(DbConnection connection)
    {
        // Registered for supplied connections too, matching the previous behaviour.
        if (connection is SqliteConnection sqliteConnection)
            SQLiteFunctions.Register(sqliteConnection);
    }

    public override ISqlDialect Dialect => SqliteDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // Microsoft.Data.Sqlite throws "Value must be set." when a parameter holds null,
        // so unset/null values must be passed as DBNull.
        return new SqliteParameter(name, value ?? DBNull.Value);
    }
}
