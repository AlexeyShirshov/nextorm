using System.Linq.Expressions;

namespace nextorm.core;

public class CommandBuilder<TEntity>
{
    private readonly SqlClient _sqlClient;

    public CommandBuilder(SqlClient sqlClient)
    {
        _sqlClient = sqlClient;
    }

    public SqlCommand<T> Select<T>(Expression<Func<TEntity, T>> exp)
    {
        return new SqlCommand<T>(_sqlClient,exp);
    }
}

public class CommandBuilder
{
    private readonly SqlClient _sqlClient;
    private readonly string _table;

    public CommandBuilder(SqlClient sqlClient, string table)
    {
        _sqlClient = sqlClient;
        _table = table;
    }

    public SqlCommand<T> Select<T>(Expression<Func<TableAlias, T>> exp)
    {
        return new SqlCommand<T>(_sqlClient,exp) {From = new FromExpression {TableName = _table}};
    }
}

public class TableAlias
{
    public int Int(string column)=>0;
    public int Long(string column)=>0;
}