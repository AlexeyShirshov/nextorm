using System.Data.Common;
using NextORM.Core;

namespace NextORM.Sqlite;

public static class SqliteDataContextOptionsBuilderExtensions
{
    public static DataContextBuilder UseSqlite(this DataContextBuilder builder, string filepath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filepath);

#if DEBUG
        if (!File.Exists(filepath))
            throw new ArgumentException($"File '{filepath}' does not exist");
#endif

        builder.Factory = (b) => new SqliteDataContext($"Data Source='{filepath}'", b);
        return builder;
    }
    public static DataContextBuilder UseSqlite(this DataContextBuilder builder, DbConnection connection)
    {
        builder.Factory = (b) => new SqliteDataContext(connection, b);

        return builder;
    }
}