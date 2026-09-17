using System.Data.Common;
using nextorm.core;
using Npgsql;

namespace nextorm.postgres;

public class PostgresDbContext : DbContext
{
    public PostgresDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public PostgresDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    protected override DbConnection CreateDbConnection(string? connectionString)
        => new NpgsqlConnection(connectionString);

    public override ISqlDialect Dialect => PostgresDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // Npgsql rejects a null parameter value, so unset/null values must be passed as DBNull.
        return new NpgsqlParameter(name, value ?? DBNull.Value);
    }
}
