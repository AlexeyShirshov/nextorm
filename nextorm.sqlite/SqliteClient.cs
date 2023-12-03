using System.Data.Common;
using System.Data.SQLite;
using System.Text;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlite;

public class SqliteDbContext : DbContext
{
    private readonly string _connectionString;

    public SqliteDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(optionsBuilder)
    {
        _connectionString = connectionString;
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
    protected override void MakePage(Paging paging, StringBuilder sb)
    {
        // var sb = _sbPool.Get();
        // try
        // {
        sb.Append("limit ").Append(paging.Limit > 0
            ? paging.Limit
            : -1);

        if (paging.Offset > 0)
            sb.Append(" offset ").Append(paging.Offset);

        //     return sb.ToString();
        // }
        // finally
        // {
        //     _sbPool.Return(sb);
        // }
    }
}