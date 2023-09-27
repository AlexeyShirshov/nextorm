using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity>
{
    private readonly IDataProvider _dataProvider;
    private readonly QueryCommand? _query;
    private Expression? _condition;

    internal ILogger? Logger { get; set; }
    public CommandBuilder(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    public CommandBuilder(IDataProvider dataProvider, QueryCommand<TEntity> query)
    {
        _dataProvider = dataProvider;
        _query = query;
    }
    public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = _dataProvider.CreateCommand<TResult>(exp, _condition);
        cmd.Logger = Logger;

        if (_query is not null)
            cmd.From = new FromExpression(_query);

        OnCommandCreated(cmd);
        
        return cmd;
    }

    protected virtual void OnCommandCreated<T>(QueryCommand<T> cmd)
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
    private readonly SqlDataProvider _dataProvider;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    private Expression? _condition;
    public CommandBuilder(SqlDataProvider dataProvider, string table)
    {
        _dataProvider = dataProvider;
        _table = table;
    }
    public QueryCommand<T> Select<T>(Expression<Func<TableAlias, T>> exp)
    {
        if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        return new QueryCommand<T>(_dataProvider, exp, _condition) { From = new FromExpression(_table), Logger = Logger };
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