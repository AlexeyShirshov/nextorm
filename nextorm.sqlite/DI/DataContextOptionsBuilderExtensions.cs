using System.Configuration;
using nextorm.core;

namespace nextorm.sqlite;

public static class DataContextOptionsBuilderExtensions
{
    public static DbContextBuilder UseSqlite(this DbContextBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

#if DEBUG
        if (!File.Exists(filepath))
            throw new ArgumentException($"File '{filepath}' does not exist");
#endif

        builder.Factory = (b) => new SqliteDbContext($"Data Source='{filepath}'", b);
        return builder;
    }
}