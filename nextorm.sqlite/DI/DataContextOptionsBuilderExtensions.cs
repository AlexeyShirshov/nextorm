using System.Data.Common;
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
    public static DbContextBuilder UseSqlite(this DbContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqliteDbContext(connection, b);

        return builder;
    }
}