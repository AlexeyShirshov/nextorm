using System.Data.Common;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MySql;

public class MySqlDataContext : DataContext
{
    public MySqlDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public MySqlDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    protected override DbConnection CreateDbConnection(string? connectionString)
        => new MySqlConnection(connectionString);

    public override ISqlDialect Dialect => MySqlDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // MySqlConnector rejects a null parameter value, so unset/null values must be passed as DBNull.
        return new MySqlParameter(name, value ?? DBNull.Value);
    }
}
