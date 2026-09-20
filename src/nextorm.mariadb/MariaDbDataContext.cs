using System.Data.Common;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

public class MariaDbDataContext : MySqlDataContext
{
    public MariaDbDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, optionsBuilder)
    {
    }

    public MariaDbDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(connection, optionsBuilder)
    {
    }

    public override ISqlDialect Dialect => MariaDbDialect.Instance;
}
