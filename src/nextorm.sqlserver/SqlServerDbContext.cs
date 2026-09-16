using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlserver;

public class SqlServerDbContext : DbContext
{
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo ChangeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;

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
        // A null value would leave SqlParameter unset, and SqlClient then sends no value at all
        // ("expects the parameter ... which was not supplied"), so nulls must be passed as DBNull.
        return new SqlParameter(name, value ?? DBNull.Value);
    }
    public override string MakeParam(string name)
    {
        return $"@{name}";
    }
    /// <summary>
    /// SQL Server bracket-quotes identifiers. Single quoted aliases (the base default) are accepted
    /// for columns but produce a syntax error for table and derived table aliases.
    /// </summary>
    public override string Escape(string keyword)
    {
        return "[" + keyword + "]";
    }
    /// <summary>
    /// SQL Server uses a bracket-quoted identifier for references as well, so that aliases that
    /// collide with a T-SQL keyword (e.g. "double") stay usable from an outer query.
    /// </summary>
    public override string MakeColumnReference(string name) => Escape(name);
    /// <summary>
    /// A SQL Server derived table (subquery in FROM) must have an alias.
    /// </summary>
    public override bool RequireSubqueryAlias => true;
    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "tinyint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "float",
        _ when type == typeof(decimal) => "decimal(38, 10)",
        _ => base.MakeTypeName(type)
    };
    public override string MakeSubqueryPredicate(string keyword, string query, bool asPredicate)
    {
        // SQL Server has no boolean type: EXISTS/ANY/ALL are valid only as a predicate, while a
        // scalar projection needs a CASE (a comparison is not a valid select list entry in T-SQL).
        if (keyword is "exists" or "any" or "all")
            return asPredicate
                ? base.MakeSubqueryPredicate(keyword, query, true)
                // The CASE literals are ints, so it is cast to bit to keep GetBoolean working.
                : $"cast(case when {base.MakeSubqueryPredicate(keyword, query, false)} then 1 else 0 end as bit)";

        return base.MakeSubqueryPredicate(keyword, query, asPredicate);
    }
    public override string MakeBoolCoalesce(string v1, string v2)
    {
        // A bit expression is not a valid predicate in T-SQL, so compare it with 1; the result is
        // still a bit value and therefore remains usable as a projection.
        return $"({MakeCoalesce(v1, v2)}) = 1";
    }
    public override Expression MapColumnExpression(SelectExpression column, Expression param)
    {
        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        if (!IsNumeric(type))
            return base.MapColumnExpression(column, param);

        // SqlClient typed getters are strict: reading an int column through GetInt64 throws, and a
        // computed numeric expression can be wider than the projected CLR type. Read the value and
        // convert it instead of relying on the reader getter to widen the type.
        var index = Expression.Constant(column.Index);
        var value = Expression.Convert(
            Expression.Call(ChangeTypeMethod, Expression.Call(param, GetValueMethod, index), Expression.Constant(type)),
            column.PropertyType);

        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, index),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        return value;
    }
    private static bool IsNumeric(Type type) => type == typeof(byte) || type == typeof(short)
        || type == typeof(int) || type == typeof(long) || type == typeof(float)
        || type == typeof(double) || type == typeof(decimal);
    public override string MakeCount(bool distinct, bool big)
    {
        // count_big returns bigint while count returns int; the SQL Server provider can express both.
        if (big)
            return distinct ? "count_big(distinct " : "count_big(";

        return base.MakeCount(distinct, false);
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
