using System.Linq.Expressions;

namespace nextorm.core;

public class CommandBuilder<TEntity>
{
    private readonly SqlClient _sqlClient;
    private readonly SqlCommand? _query;
    public CommandBuilder(SqlClient sqlClient)
    {
        _sqlClient = sqlClient;
    }
    public CommandBuilder(SqlClient sqlClient, SqlCommandFinal<TEntity> query)
    {
        _sqlClient = sqlClient;
        _query = query;
    }
    public SqlCommandFinal<T> Select<T>(Expression<Func<TEntity, T>> exp)
    {
        if (_query is null)
            return new SqlCommandFinal<T>(_sqlClient,exp);
        
        return new SqlCommandFinal<T>(_sqlClient,exp) {From = new FromExpression(_query)};
    }
}
public class CommandBuilder
{
    private readonly SqlClient _sqlClient;
    private readonly string? _table;
    public CommandBuilder(SqlClient sqlClient, string table)
    {
        _sqlClient = sqlClient;
        _table = table;
    }
    public SqlCommandFinal<T> Select<T>(Expression<Func<TableAlias, T>> exp)
    {
        if (string.IsNullOrEmpty(_table)) throw new InvalidOperationException("Table must be specified");
        return new SqlCommandFinal<T>(_sqlClient,exp) {From = new FromExpression(_table)};
    }
}

public class TableAlias
{
    public int Int(string column)=>0;
    public int Long(string column)=>0;
}