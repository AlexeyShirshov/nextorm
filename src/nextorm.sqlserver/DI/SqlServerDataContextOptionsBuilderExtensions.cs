using System.Data.Common;
using NextORM.Core;

namespace NextORM.SqlServer;

public static class SqlServerDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UseSqlServer(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new SqlServerDataContext(connectionString, b);

        return builder;
    }
    public static DataContextBuilder UseSqlServer(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqlServerDataContext(connection, b);

        return builder;
    }
}