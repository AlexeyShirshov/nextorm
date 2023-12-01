using System.Configuration;
using nextorm.core;

namespace nextorm.sqlite;

public static class DataContextOptionsBuilderExtensions
{
    public const string ConnectionString = "sqlite:conn";
    public static DataContextOptionsBuilder UseSqlite(this DataContextOptionsBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

#if DEBUG
        if (!File.Exists(filepath))
            throw new ArgumentException($"File '{filepath}' does not exist");
#endif

        builder.Property[ConnectionString] = $"Data Source='{filepath}'";
        return builder;
    }
}