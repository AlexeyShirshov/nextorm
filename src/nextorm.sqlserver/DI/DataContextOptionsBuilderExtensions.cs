using System.Data.Common;
using nextorm.core;

namespace nextorm.sqlserver;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseSqlServer(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new SqlServerDbContext(connectionString, b);

        return builder;
    }
    public static DbContextBuilder UseSqlServer(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqlServerDbContext(connection, b);

        return builder;
    }
}