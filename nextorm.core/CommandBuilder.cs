using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class CommandBuilder<TEntity>
{
    private readonly SqlClient _sqlClient;
    private readonly SqlCommand? _query;
    internal ILogger? Logger { get; set; }
    public CommandBuilder(SqlClient sqlClient)
    {
        _sqlClient = sqlClient;
    }
    public CommandBuilder(SqlClient sqlClient, SqlCommand<TEntity> query)
    {
        _sqlClient = sqlClient;
        _query = query;
    }
    public SqlCommand<T> Select<T>(Expression<Func<TEntity, T>> exp)
    {
        if (_query is null)
            return new SqlCommand<T>(_sqlClient,exp);
        
        return new SqlCommand<T>(_sqlClient,exp) {From = new FromExpression(_query), Logger = Logger};
    }
}
public class CommandBuilder
{
    private readonly SqlClient _sqlClient;
    private readonly string? _table;
    internal ILogger? Logger { get; set; }
    public CommandBuilder(SqlClient sqlClient, string table)
    {
        _sqlClient = sqlClient;
        _table = table;
    }
    public SqlCommand<T> Select<T>(Expression<Func<TableAlias, T>> exp)
    {
        if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        return new SqlCommand<T>(_sqlClient,exp) {From = new FromExpression(_table), Logger = Logger};
    }
}

public class TableAlias
{
    public int Int(string _)=>0;
    public long Long(string _)=>0;
    public short Short(string _)=>0;
    public string String(string _)=>string.Empty;
    public float Float(string _)=>0;
    public double Double(string _)=>0;
    public DateTime DateTime(string _)=>System.DateTime.MinValue;
    public decimal Decimal(string _)=>0;
    public byte Byte(string _)=>0;
    public bool Boolean(string _)=>false;
    public Guid Guid(string _)=>System.Guid.Empty;
    public int? NullableInt(string _)=>0;
    public long? NullableLong(string _)=>0;
    public short? NullableShort(string _)=>0;
    public string? NullableString(string _)=>string.Empty;
    public float? NullableFloat(string _)=>0;
    public double? NullableDouble(string _)=>0;
    public DateTime? NullableDateTime(string _)=>System.DateTime.MinValue;
    public decimal? NullableDecimal(string _)=>0;
    public byte? NullableByte(string _)=>0;
    public bool? NullableBoolean(string _)=>false;
    public Guid? NullableGuid(string _)=>System.Guid.Empty;
}