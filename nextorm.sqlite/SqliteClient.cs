using System.Data.Common;
using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlite;

public class SqliteDbContext : DbContext
{
    private readonly string _connectionString;

    public SqliteDbContext(DataContextOptionsBuilder optionsBuilder)
        : base(optionsBuilder)
    {
        _connectionString = (string)optionsBuilder.Property[DataContextOptionsBuilderExtensions.ConnectionString];
    }

    public override DbConnection CreateConnection()
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            if (LogSensetiveData)
                Logger.LogDebug("Creating connection with {connStr}", _connectionString);
            else
                Logger.LogDebug("Creating connection");
        }

        return new SQLiteConnection(_connectionString);
    }
    public override DbCommand CreateCommand(string sql)
    {
        return new SQLiteCommand(sql);
    }
    public override string ConcatStringOperator => "||";

    public string ConnectionString => _connectionString;

    public override string MakeCoalesce(string v1, string v2)
    {
        return $"ifnull({v1}, {v2})";
    }
    public override string MakeParam(string name)
    {
        return $"${name}";
    }
    public override DbParameter CreateParam(string name, object? value)
    {
        return new SQLiteParameter(name, value);
    }
}