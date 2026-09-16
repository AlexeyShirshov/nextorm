using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Text;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlite;

public class SqliteDbContext : DbContext
{
    private readonly string? _connectionString;
    private readonly DbConnection? _connection;
    public SqliteDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(optionsBuilder)
    {
        _connectionString = connectionString;
    }
    public SqliteDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
            : base(optionsBuilder)
    {
        _connection = connection;
    }
    public override DbConnection CreateConnection()
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            if (LogSensitiveData)
                Logger.LogDebug("Creating connection with {connStr}", _connectionString);
            else
                Logger.LogDebug("Creating connection");
        }

        if (_connection is not null)
        {
            _connWasCreatedByMe = false;
            if (_connection is SqliteConnection providedConnection)
                SQLiteFunctions.Register(providedConnection);
            return _connection;
        }

        var conn = new SqliteConnection(_connectionString);
        SQLiteFunctions.Register(conn);
        return conn;
    }
    // public override DbCommand CreateCommand(string sql)
    // {
    //     return new SqliteCommand(sql);
    // }
    public override string ConcatStringOperator => "||";

    public string ConnectionString => string.IsNullOrEmpty(_connectionString)
        ? _connection!.ConnectionString
        : _connectionString!;

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
        // Microsoft.Data.Sqlite throws "Value must be set." when a parameter holds null,
        // so unset/null values must be passed as DBNull.
        return new SqliteParameter(name, value ?? DBNull.Value);
    }
    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // var sb = _sbPool.Get();
        // try
        // {
        sqlBuilder.Append("limit ").Append(paging.Limit > 0
            ? paging.Limit
            : -1);

        if (paging.Offset > 0)
            sqlBuilder.Append(" offset ").Append(paging.Offset);

        //     return sb.ToString();
        // }
        // finally
        // {
        //     _sbPool.Return(sb);
        // }
    }
    public override string MakeCount(bool distinct, bool big)
    {
        if (big) throw new NotSupportedException("count big is not supported");

        return base.MakeCount(distinct, false);
    }

}
