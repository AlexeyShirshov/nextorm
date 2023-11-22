#define PARAM_CONDITION
using System.Buffers;
using System.Data;
using System.Data.Common;

namespace nextorm.core;
public class CompiledQuery<TResult, TRecord>
{
    public readonly Func<TRecord, TResult> MapDelegate;

    public CompiledQuery(Func<TRecord, TResult> mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    public CompiledQuery(Func<Func<TRecord, TResult>> getMap)
    {
        MapDelegate = getMap();
    }
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public TResult Map(object dataRecord)
    // {
    //     return MapDelegate(dataRecord);
    // }
}
public class InMemoryCompiledQuery<TResult, TEntity> : CompiledQuery<TResult, TEntity>
{
#if PARAM_CONDITION
    public readonly Func<TEntity, object[]?, bool>? Condition;
#else
    public readonly Func<TEntity, bool>? Condition;
#endif
    public InMemoryCompiledQuery(Func<Func<TEntity, TResult>> func, Func<TEntity, object[]?, bool>? condition)
        : base(func)
    {
        Condition = condition;
        // #if PARAM_CONDITION
        //         if (condition is not null)
        //         {
        //             var p = Expression.Parameter(typeof(object[]));
        //             var replaceParam = new ParamExpressionVisitor2(p);
        //             var lambda = (LambdaExpression)replaceParam.Visit(condition)!;
        //             // if (replaceParam.Converted)
        //             // {
        //             var @params = new List<ParameterExpression>(lambda.Parameters) { p };
        //             Condition = Expression.Lambda<Func<TEntity, object[]?, bool>>(lambda.Body, @params).Compile();
        //             // }
        //             // else
        //             //     Condition = condition?.Compile();
        //         }
        // #else
        //         Condition = condition?.Compile();
        // #endif
    }
}
public class DatabaseCompiledQuery<TResult> : CompiledQuery<TResult, IDataRecord>//, IReplaceParam
{
    public readonly DbCommand DbCommand;
    public readonly System.Data.CommandBehavior Behavior = System.Data.CommandBehavior.SingleResult;
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<IDataRecord, TResult>> getMap)
        : base(getMap)
    {
        DbCommand = dbCommand;
    }
    public DatabaseCompiledQuery(DbCommand dbCommand, Func<IDataRecord, TResult> mapDelegate, bool singleRow = false)
        : base(mapDelegate)
    {
        DbCommand = dbCommand;
        if (singleRow)
            Behavior = System.Data.CommandBehavior.SingleResult | System.Data.CommandBehavior.SingleRow;
    }

    // public void ReplaceParams(object[] @params, IDataProvider dataProvider)
    // {
    //     //if (dataProvider is SqlDataProvider sqlDataProvider)
    //     for (var i = 0; i < @params.Length; i++)
    //     {
    //         var paramName = string.Format("norm_p{0}", i);
    //         foreach (DbParameter p in DbCommand.Parameters)
    //         {
    //             if (p.ParameterName == paramName)
    //             {
    //                 p.Value = @params[i];
    //                 break;
    //             }
    //         }
    //     }
    // }
}
public class DatabaseCompiledPlan<TResult> : CompiledQuery<TResult, IDataRecord>
{
    internal readonly string _sql;
    public readonly bool NoParams;
    public DatabaseCompiledPlan(string sql, Func<Func<IDataRecord, TResult>> getMap, bool noParams)
        : base(getMap)
    {
        _sql = sql;
        NoParams = noParams;
    }
    public DatabaseCompiledPlan(string sql, Func<IDataRecord, TResult> map, bool noParams)
        : base(map)
    {
        _sql = sql;
        NoParams = noParams;
    }
    public DbCommand GetCommand(List<Param> @params, SqlDataProvider dataProvider)
    {
        var dbCommand = dataProvider.CreateCommand(_sql);

        if (!NoParams)
            dbCommand.Parameters.AddRange(@params.Select(it => dataProvider.CreateParam(it.Name, it.Value)).ToArray());

        return dbCommand;
    }
    public DatabaseCompiledQuery<TResult> CompileQuery(List<Param> @params, SqlDataProvider dataProvider)
        => new(GetCommand(@params, dataProvider), MapDelegate);
}