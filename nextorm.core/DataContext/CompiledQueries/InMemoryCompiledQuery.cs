#define PARAM_CONDITION
namespace nextorm.core;

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
