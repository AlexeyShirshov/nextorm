using System.Data.Common;
using nextorm.core;
using nextorm.mysql;

namespace nextorm.mariadb;

public class MariaDbContext : MySqlDbContext
{
    public MariaDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    public MariaDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
        : base(connection, optionsBuilder)
    {
    }

    public override ISqlDialect Dialect => MariaDbDialect.Instance;
}
