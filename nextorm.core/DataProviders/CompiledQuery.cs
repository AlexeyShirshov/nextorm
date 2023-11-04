using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;
public class CompiledQuery<TResult>
{
    public readonly Func<object, TResult> MapDelegate;
    //private readonly Func<Func<object, TResult>> _getMap;
    public CompiledQuery(Func<Func<object, TResult>> getMap)
    {
        MapDelegate = getMap();
    }
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public TResult Map(object dataRecord)
    // {
    //     return MapDelegate(dataRecord);
    // }
}
public class InMemoryCompiledQuery<TResult, TEntity> : CompiledQuery<TResult>
{
    public readonly Func<TEntity, bool>? Condition;
    public InMemoryCompiledQuery(Func<Func<object, TResult>> func, Expression<Func<TEntity, bool>>? condition)
        : base(func)
    {
        Condition = condition?.Compile();
    }
}
public class DatabaseCompiledQuery<TResult> : CompiledQuery<TResult>
{
    private readonly DbCommand _dbCommand;
    public readonly bool NoParams;
    public DbCommand GetCommand(List<Param> @params, SqlDataProvider dataProvider)
    {
        if (!NoParams)
        {
            _dbCommand.Parameters.Clear();
            _dbCommand.Parameters.AddRange(@params.Select(it => dataProvider.CreateParam(it.Name, it.Value)).ToArray());
        }
        return _dbCommand;
    }
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<object, TResult>> getMap, bool noParams)
        : base(getMap)
    {
        _dbCommand = dbCommand;
        NoParams = noParams;
    }
}