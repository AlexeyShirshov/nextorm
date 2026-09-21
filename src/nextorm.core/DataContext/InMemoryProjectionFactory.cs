using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Projection axis of the in-memory join pipeline: compiles the join condition and materialises the
/// <c>Projection&lt;T1..T8&gt;</c> rows a multi-table join accumulates. Pure static helpers — no
/// instance state and no reflection-addressed entry points (those stay on
/// <see cref="InMemoryDataContext"/>). Bodies are moved verbatim from <see cref="InMemoryDataContext"/>
/// (F13 follow-up).
/// </summary>
internal static class InMemoryProjectionFactory
{
    public static Func<TLeft, TRight, bool> CompileJoinCondition<TLeft, TRight>(JoinExpression join)
        => ((Expression<Func<TLeft, TRight, bool>>)join.JoinCondition!).Compile();

    public static Type CreateProjectionType(Type firstType, Type secondType, int dim)
    {
        var typeName = $"NextORM.Core.Projection`{dim}";
        var t = typeof(InMemoryDataContext).Assembly.GetType(typeName) ?? throw new InvalidOperationException($"Cannot create type {typeName}");
        var types = dim switch
        {
            2 => new List<Type> { firstType, secondType },
            >= 3 => new List<Type>(firstType.GetGenericArguments()) { secondType },
            _ => throw new NotImplementedException(dim.ToString())
        };

        return t.MakeGenericType(types.ToArray());
    }

    public static IProjection CreateProjection<TLeft, TRight>(TLeft left, TRight right, int dim)
    {
        if (dim == 2) return new Projection<TLeft, TRight> { Item1 = left, Item2 = right };
        if (left is IExtendableProjection proj)
        {
            return proj.Extend(right);
            // var (types, values) = ExtractTypesFromProjection(left);
            // types.Add(typeof(TRight));
            // var typeName = $"NextORM.Core.Projection`{dim}";
            // var t = Type.GetType(typeName)!;
            // var prjType = t.MakeGenericType(types.ToArray());
            // values.Add(right!);

            // //var leftType = typeof(TLeft);

            // var bindings = values.Select((value, idx) =>
            // {
            //     var propInfo = prjType.GetProperty("Item" + (idx + 1).ToString())!;
            //     return Expression.Bind(propInfo, Expression.Constant(value));
            // }).ToArray();

            // var ctor = Expression.New(prjType.GetConstructor(Type.EmptyTypes)!);

            // var memberInit = Expression.MemberInit(ctor, bindings);

            // var lambda = Expression.Lambda(memberInit);

            // return lambda.Compile().DynamicInvoke()!;
        }
        if (left is null && dim >= 3)
        {
            // A RIGHT/FULL join matched no accumulated row on this side: the whole left-hand
            // projection is absent, so materialize a projection whose earlier items keep their
            // defaults and whose last item is the joined entity. Reflection is only paid on this
            // (unmatched) path.
            var prjType = CreateProjectionType(typeof(TLeft), typeof(TRight), dim);
            var projection = (IProjection)Activator.CreateInstance(prjType)!;
            prjType.GetProperty("Item" + dim)!.SetValue(projection, right);
            return projection;
        }

        throw new NotSupportedException($"Joins of dimension {dim} are not supported");

        // static (List<Type>, List<object?>) ExtractTypesFromProjection(TLeft projection)
        // {
        //     var types = new List<Type>();
        //     var values = new List<object?>();
        //     var leftType = typeof(TLeft);
        //     foreach (var propInfo in leftType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        //     {
        //         types.Add(propInfo.PropertyType);
        //         values.Add(propInfo.GetValue(projection));
        //     }
        //     return (types, values);
        // }
    }
}
