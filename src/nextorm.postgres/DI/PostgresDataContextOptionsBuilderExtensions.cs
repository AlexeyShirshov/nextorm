using System.Data.Common;
using NextORM.Core;

namespace NextORM.Postgres;

public static class PostgresDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UsePostgres(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new PostgresDataContext(connectionString, b);

        return builder;
    }
    public static DataContextBuilder UsePostgres(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new PostgresDataContext(connection, b);

        return builder;
    }
}
