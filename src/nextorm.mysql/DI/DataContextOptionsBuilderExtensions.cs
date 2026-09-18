using System.Data.Common;
using nextorm.core;

namespace nextorm.mysql;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseMySql(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new MySqlDbContext(connectionString, b);

        return builder;
    }
    public static DbContextBuilder UseMySql(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new MySqlDbContext(connection, b);

        return builder;
    }
}
