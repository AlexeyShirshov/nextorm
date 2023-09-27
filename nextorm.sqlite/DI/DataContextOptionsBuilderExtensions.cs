using nextorm.core;

namespace nextorm.sqlite;

public static class DataContextOptionsBuilderExtensions
{
    public static DataContextOptionsBuilder UseSqlite(this DataContextOptionsBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

        builder.UseSqlClient(new SqliteDataProvider($"Data Source='{filepath}'"));
        return builder;
    }
}