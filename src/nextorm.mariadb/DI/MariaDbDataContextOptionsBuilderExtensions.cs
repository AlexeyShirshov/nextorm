using System.Data.Common;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

public static class MariaDbDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UseMariaDb(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MariaDbDataContext(connectionString, b);

        return builder;
    }
    public static DataContextBuilder UseMariaDb(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MariaDbDataContext(connection, b);

        return builder;
    }
}
