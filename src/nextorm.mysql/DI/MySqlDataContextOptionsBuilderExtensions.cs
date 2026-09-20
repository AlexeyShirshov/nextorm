using System.Data.Common;
using NextORM.Core;

namespace NextORM.MySql;

public static class MySqlDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UseMySql(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MySqlDataContext(connectionString, b);

        return builder;
    }
    public static DataContextBuilder UseMySql(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MySqlDataContext(connection, b);

        return builder;
    }
}
