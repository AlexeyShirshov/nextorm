using System.Data.Common;
using nextorm.core;

namespace nextorm.postgres;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UsePostgres(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new PostgresDbContext(connectionString, b);

        return builder;
    }
    public static DbContextBuilder UsePostgres(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new PostgresDbContext(connection, b);

        return builder;
    }
}
