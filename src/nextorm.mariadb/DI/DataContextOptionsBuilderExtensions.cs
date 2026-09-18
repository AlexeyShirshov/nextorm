using System.Data.Common;
using nextorm.core;
using nextorm.mysql;

namespace nextorm.mariadb;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseMariaDb(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MariaDbContext(connectionString, b);

        return builder;
    }
    public static DbContextBuilder UseMariaDb(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MariaDbContext(connection, b);

        return builder;
    }
}
