using nextorm.core;

namespace nextorm.sqlserver;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseSqlServer(this DbContextBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.Factory = (b) => new SqlServerClient(connectionString, b);

        return builder;
    }
}