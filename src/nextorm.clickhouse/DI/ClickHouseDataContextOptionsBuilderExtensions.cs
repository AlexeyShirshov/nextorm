using System.Data.Common;
using NextORM.Core;

namespace NextORM.ClickHouse;

public static class ClickHouseDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new ClickHouseDataContext(connectionString, b);

        return builder;
    }
    public static DataContextBuilder UseClickHouse(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new ClickHouseDataContext(connection, b);

        return builder;
    }
}
