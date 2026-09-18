using System.Data.Common;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using nextorm.core;

namespace nextorm.clickhouse;

public class ClickHouseDbContext : DbContext
{
    public ClickHouseDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public ClickHouseDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    protected override DbConnection CreateDbConnection(string? connectionString)
        => new ClickHouseConnection(connectionString);

    public override ISqlDialect Dialect => ClickHouseDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // The driver rewrites the ADO-style @name placeholder to {name:Type} and infers the
        // ClickHouse type from the CLR value; the stored name is the one without the @ prefix.
        return new ClickHouseDbParameter
        {
            ParameterName = name.TrimStart('@'),
            Value = value
        };
    }
}
