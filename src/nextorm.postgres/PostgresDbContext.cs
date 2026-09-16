using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using nextorm.core;
using Npgsql;

namespace nextorm.postgres;

public class PostgresDbContext : DbContext
{
    private readonly string? _connectionString;
    private readonly DbConnection? _connection;

    public PostgresDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(optionsBuilder)
    {
        _connectionString = connectionString;
    }

    public PostgresDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
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

        return new NpgsqlConnection(_connectionString);
    }

    public string ConnectionString => string.IsNullOrEmpty(_connectionString)
        ? _connection!.ConnectionString
        : _connectionString!;

    public override string ConcatStringOperator => "||";

    public override DbParameter CreateParam(string name, object? value)
    {
        // Npgsql rejects a null parameter value, so unset/null values must be passed as DBNull.
        return new NpgsqlParameter(name, value ?? DBNull.Value);
    }

    public override string MakeParam(string name)
    {
        return $"@{name}";
    }

    public override string MakeCoalesce(string v1, string v2)
    {
        return $"coalesce({v1}, {v2})";
    }

    public override string MakeBool(bool v)
    {
        return v ? "true" : "false";
    }

    public override string Escape(string keyword)
    {
        // PostgreSQL only accepts double-quoted identifiers; single-quoted aliases are a syntax error.
        return "\"" + keyword + "\"";
    }

    public override string MakeColumnReference(string name)
    {
        return Escape(name);
    }

    public override bool RequireSubqueryAlias => true;

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev",
        "stdevp" => "stddev_pop",
        "var" => "variance",
        "varp" => "var_pop",
        _ => name
    };

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // PostgreSQL uses "limit N offset M"; OFFSET may appear on its own, but LIMIT must come first.
        if (paging.Limit > 0)
            sqlBuilder.Append("limit ").Append(paging.Limit);

        if (paging.Offset > 0)
        {
            if (paging.Limit > 0)
                sqlBuilder.Append(' ');

            sqlBuilder.Append("offset ").Append(paging.Offset);
        }
    }
}
