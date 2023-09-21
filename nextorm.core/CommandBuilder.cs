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
    public int Int(string column)=>0;
    public int Long(string column)=>0;
}