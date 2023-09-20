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
        return new SqlCommand<T>(_sqlClient);
    }
}