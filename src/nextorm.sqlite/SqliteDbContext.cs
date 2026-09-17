using System.Data.Common;
using Microsoft.Data.Sqlite;
using nextorm.core;

namespace nextorm.sqlite;

public class SqliteDbContext : DbContext
{
    public SqliteDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }
    public SqliteDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
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
