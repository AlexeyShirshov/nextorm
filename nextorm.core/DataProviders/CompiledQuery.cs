//#define INITALGO_2

using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;
public class CompiledQuery<TResult>
{
#if INITALGO_2
    public readonly Func<object, object[]?, TResult> MapDelegate;
    public CompiledQuery(Func<object, object[]?, TResult> mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    public CompiledQuery(Func<Func<object, object[]?, TResult>> getMap)
    {
        MapDelegate = getMap();
    }
#else
    public readonly Func<object, TResult> MapDelegate;

    public CompiledQuery(Func<object, TResult> mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    public CompiledQuery(Func<Func<object, TResult>> getMap)
    {
        MapDelegate = getMap();
    }
#endif
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public TResult Map(object dataRecord)
    // {
    //     return MapDelegate(dataRecord);
    // }
}
public class InMemoryCompiledQuery<TResult, TEntity> : CompiledQuery<TResult>
{
    public readonly Func<TEntity, bool>? Condition;
#if INITALGO_2
    public InMemoryCompiledQuery(Func<Func<object, object[]?, TResult>> func, Expression<Func<TEntity, bool>>? condition)
        : base(func)
#else
    public InMemoryCompiledQuery(Func<Func<object, TResult>> func, Expression<Func<TEntity, bool>>? condition)
        : base(func)
#endif
    {
        Condition = condition?.Compile();
    }
}
public class DatabaseCompiledQuery<TResult> : CompiledQuery<TResult>
{
    public readonly DbCommand DbCommand;
#if INITALGO_2
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<object, object[]?, TResult>> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
    }
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<object, object[]?, TResult> mapDelegate)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
    }
#else
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<object, TResult>> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
    }
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<object, TResult> mapDelegate)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
    }
#endif
}
public class DatabaseCompiledPlan<TResult> : CompiledQuery<TResult>
{
    internal readonly string _sql;
    public readonly bool NoParams;
#if INITALGO_2
    public DatabaseCompiledPlan(string sql, Func<Func<object, object[]?, TResult>> getMap, bool noParams)
        : base(getMap)
    {
        _sql = sql;
        NoParams = noParams;
    }
    public DatabaseCompiledPlan(string sql, Func<object, object[]?, TResult> map, bool noParams)
        : base(map)
    {
        _sql = sql;
        NoParams = noParams;
    }
#else
    public DatabaseCompiledPlan(string sql, Func<Func<object, TResult>> getMap, bool noParams)
        : base(getMap)
    {
        _sql = sql;
        NoParams = noParams;
    }
    public DatabaseCompiledPlan(string sql, Func<object, TResult> map, bool noParams)
        : base(map)
    {
        _sql = sql;
        NoParams = noParams;
    }
#endif
    public DbCommand GetCommand(List<Param> @params, SqlDataProvider dataProvider)
    {
        var dbCommand = dataProvider.CreateCommand(_sql);

        if (!NoParams)
            dbCommand.Parameters.AddRange(@params.Select(it => dataProvider.CreateParam(it.Name, it.Value)).ToArray());

        return dbCommand;
    }
    public DatabaseCompiledQuery<TResult> CompileQuery(List<Param> @params, SqlDataProvider dataProvider)
        => new DatabaseCompiledQuery<TResult>(GetCommand(@params, dataProvider), MapDelegate);
}