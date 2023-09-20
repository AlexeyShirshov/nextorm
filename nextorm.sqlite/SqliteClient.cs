using System.Data.Common;
using System.Data.SQLite;
using nextorm.core;

namespace nextorm.sqlite;

public class SqliteClient : SqlClient
{
    private readonly string _connectionString;

    public SqliteClient(string connectionString)
    {
        _connectionString = connectionString;
    }

    public override DbConnection CreateConnection()
    {
        return new SQLiteConnection(_connectionString);
    }
    public override DbCommand CreateCommand(string sql)
    {
        return new SQLiteCommand(sql);
    }
}