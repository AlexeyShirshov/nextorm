using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Builds the body of a row-materializer expression that is shared by the database
/// (<see cref="DataContext"/>) and in-memory (<see cref="InMemoryDataContext"/>) contexts.
/// The two providers differ only in the record type they read from and in how a single
/// column is mapped, so the "result shape -> expression tree" knowledge lives here
/// instead of being copied into both contexts (see docs/specs/design/solid-review.md, F4).
/// </summary>
internal static class RowMaterializerBuilder
{
    /// <summary>
    /// Picks the longest constructor of <paramref name="resultType"/> and returns either a
    /// constructor call (when it takes exactly one argument per selected column) or a
    /// member-init expression that assigns each property by name. When
    /// <paramref name="ignoreColumns"/> is set (Any/Count select "*"), there is no
    /// projection to bind and an empty constructor call is returned.
    /// </summary>
    public static Expression Build(
        Type resultType,
        ParameterExpression param,
        SelectExpression[] selectList,
        bool ignoreColumns,
        Func<SelectExpression, Expression> mapColumn)
    {
        var ctorInfo = resultType.GetConstructors()
            .OrderByDescending(it => it.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new QueryPreparationException($"Cannot get ctor from {resultType}");

        if (ignoreColumns)
            return Expression.New(ctorInfo);

        if (ctorInfo.GetParameters().Length == selectList.Length)
        {
            var newParams = new Expression[selectList.Length];
            for (var i = 0; i < selectList.Length; i++)
                newParams[i] = mapColumn(selectList[i]);

            return Expression.New(ctorInfo, newParams);
        }

        var bindings = new MemberBinding[selectList.Length];
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
            bindings[i] = Expression.Bind(propInfo, mapColumn(column));
        }

        return Expression.MemberInit(Expression.New(ctorInfo), bindings);
    }
}
