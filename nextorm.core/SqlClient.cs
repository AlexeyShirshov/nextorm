using System.Data.Common;

namespace nextorm.core;

public class SqlClient
{
    public virtual DbConnection CreateConnection()
    {
        throw new NotImplementedException();
    }

    public virtual DbCommand CreateCommand(string sql)
    {
        throw new NotImplementedException();
    }
}
