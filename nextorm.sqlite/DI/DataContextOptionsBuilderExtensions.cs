using nextorm.core;

namespace nextorm.sqlite;

public static class DataContextOptionsBuilderExtensions
{
    public static DataContextOptionsBuilder UseSqlite(this DataContextOptionsBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

#if DEBUG
        if (!File.Exists(filepath))
            throw new ArgumentException($"File '{filepath}' does not exist");
#endif

        builder.UseSqlClient(new SqliteDataProvider($"Data Source='{filepath}'"));
        return builder;
    }
}