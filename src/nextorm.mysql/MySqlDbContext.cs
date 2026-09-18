using System.Data.Common;
using MySqlConnector;
using nextorm.core;

namespace nextorm.mysql;

public class MySqlDbContext : DbContext
{
    public MySqlDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public MySqlDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
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
