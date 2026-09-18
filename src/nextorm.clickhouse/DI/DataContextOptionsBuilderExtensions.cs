using System.Data.Common;
using nextorm.core;

namespace nextorm.clickhouse;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseClickHouse(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new ClickHouseDbContext(connectionString, b);

        return builder;
    }
    public static DbContextBuilder UseClickHouse(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new ClickHouseDbContext(connection, b);

        return builder;
    }
}
