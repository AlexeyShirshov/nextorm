using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity>
{
    private readonly DataProvider _sqlClient;
    private readonly IQueryCommand? _query;
    private Expression? _condition;

    internal ILogger? Logger { get; set; }
    public CommandBuilder(DataProvider sqlClient)
    {
        _sqlClient = sqlClient;
    }
    public CommandBuilder(DataProvider sqlClient, IQueryCommand<TEntity> query)
    {
        _sqlClient = sqlClient;
        _query = query;
    }
    public IQueryCommand<T> Select<T>(Expression<Func<TEntity, T>> exp)
    {
        var cmd = _sqlClient.CreateCommand<T>(exp, _condition);
        cmd.Logger = Logger;

        if (_query is not null)
            cmd.From = new FromExpression(_query as QueryCommand);

        OnCommandCreated(cmd);
        
        return cmd;
        // if (_query is null)
        //     return new SqlCommand<T>(_sqlClient, exp, _condition);

        // return new SqlCommand<T>(_sqlClient, exp, _condition) { From = new FromExpression(_query), Logger = Logger };
    }

    protected virtual void OnCommandCreated<T>(IQueryCommand<T> cmd)
    {
        
    }

    public CommandBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
    {
        if (_condition is not null)
            _condition = Expression.AndAlso(_condition, condition);
        else
            _condition = condition;

        return this;
    }
}
public class CommandBuilder
{
    private readonly SqlClient _sqlClient;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression? _condition;
    public CommandBuilder(SqlClient sqlClient, string table)
    {
        _sqlClient = sqlClient;
        _table = table;
    }
    public SqlCommand<T> Select<T>(Expression<Func<TableAlias, T>> exp)
    {
        if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        return new SqlCommand<T>(_sqlClient, exp, _condition) { From = new FromExpression(_table), Logger = Logger };
    }

    public CommandBuilder Where(Expression<Func<TableAlias, bool>> condition)
    {
        if (_condition is not null)
            _condition = Expression.AndAlso(_condition, condition);
        else
            _condition = condition;

        return this;
    }
}

public class TableAlias
{
    public int Int(string _) => 0;
    public long Long(string _) => 0;
    public short Short(string _) => 0;
    public string String(string _) => string.Empty;
    public float Float(string _) => 0;
    public double Double(string _) => 0;
    public DateTime DateTime(string _) => System.DateTime.MinValue;
    public decimal Decimal(string _) => 0;
    public byte Byte(string _) => 0;
    public bool Boolean(string _) => false;
    public Guid Guid(string _) => System.Guid.Empty;
    public int? NullableInt(string _) => 0;
    public long? NullableLong(string _) => 0;
    public short? NullableShort(string _) => 0;
    public string? NullableString(string _) => string.Empty;
    public float? NullableFloat(string _) => 0;
    public double? NullableDouble(string _) => 0;
    public DateTime? NullableDateTime(string _) => System.DateTime.MinValue;
    public decimal? NullableDecimal(string _) => 0;
    public byte? NullableByte(string _) => 0;
    public bool? NullableBoolean(string _) => false;
    public Guid? NullableGuid(string _) => System.Guid.Empty;
}