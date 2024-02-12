using System.Data.Common;
using System.Text;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlserver;

public class SqlServerDbContext : DbContext
{
    private readonly string? _connectionString;
    private readonly DbConnection? _connection;

    public SqlServerDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(optionsBuilder)
    {
        _connectionString = connectionString;
    }

    public SqlServerDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
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
            return _connection;
        }

        return new SqlConnection(_connectionString);
    }
    public string ConnectionString => string.IsNullOrEmpty(_connectionString)
        ? _connection!.ConnectionString
        : _connectionString!;
    // public override DbCommand CreateCommand(string sql)
    // {
    //     return new SqlCommand(sql) { CommandType = System.Data.CommandType.Text };
    // }
    public override DbParameter CreateParam(string name, object? value)
    {
        return new SqlParameter(name, value);
    }
    public override string MakeParam(string name)
    {
        return $"@{name}";
    }
    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("offset ").Append(paging.Offset).Append(" rows");

        if (paging.Limit > 0)
            sqlBuilder.AppendLine().Append("fetch next ").Append(paging.Limit).Append(" rows only");
    }
    public override bool RequireSorting(QueryCommand queryCommand)
    {
        return !queryCommand.Paging.IsEmpty;
    }
    public override string EmptySorting()
    {
        return "(select null as anyorder)";
    }
    public override bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = string.Format("top({0})", limit);
        return true;
    }
}
