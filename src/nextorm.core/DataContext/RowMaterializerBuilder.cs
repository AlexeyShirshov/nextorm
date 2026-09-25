using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

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
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> RangeCtorCache = new();

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
        var ctorParams = ctorInfo.GetParameters();

        if (ignoreColumns)
            return Expression.New(ctorInfo);

        var hasRangePairs = false;
        for (var i = 0; i < selectList.Length; i++)
        {
            if (selectList[i].RangeColumnRole != RangeColumnRole.None)
            {
                hasRangePairs = true;
                break;
            }
        }

        if (!hasRangePairs && ctorParams.Length == selectList.Length)
        {
            var newParams = new Expression[selectList.Length];
            for (var i = 0; i < selectList.Length; i++)
                newParams[i] = mapColumn(selectList[i]);

            return Expression.New(ctorInfo, newParams);
        }

        if (hasRangePairs && ctorParams.Length > 0)
            return BuildRangePairConstructor(resultType, ctorInfo, ctorParams, selectList, mapColumn);

        return BuildMemberInit(resultType, ctorInfo, selectList, mapColumn);
    }

    // A range pair expands into two columns but materializes as a single Range<T> value, so the
    // selected-column count no longer matches the entity constructor's parameter count. Rebuild the
    // constructor arguments by consuming each lower/upper pair as one value; when the ctor shape does
    // not line up, fall back to a parameterless ctor (member-init) or fail with a preparation error.
    private static Expression BuildRangePairConstructor(
        Type resultType,
        ConstructorInfo ctorInfo,
        ParameterInfo[] ctorParams,
        SelectExpression[] selectList,
        Func<SelectExpression, Expression> mapColumn)
    {
        var argumentCount = 0;
        for (var i = 0; i < selectList.Length; i++)
        {
            if (selectList[i].RangeColumnRole != RangeColumnRole.Upper)
                argumentCount++;
        }

        if (ctorParams.Length == argumentCount)
        {
            var arguments = new Expression[argumentCount];
            var index = 0;
            for (var i = 0; i < selectList.Length; i++)
            {
                var column = selectList[i];
                if (column.RangeColumnRole == RangeColumnRole.Upper)
                    continue;

                arguments[index++] = column.RangeColumnRole == RangeColumnRole.Lower
                    ? BuildRangeValue(column, FindUpper(selectList, i, column.PropertyInfo), mapColumn)
                    : mapColumn(column);
            }

            return Expression.New(ctorInfo, arguments);
        }

        var parameterlessCtor = resultType.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor is not null)
            return BuildMemberInit(resultType, parameterlessCtor, selectList, mapColumn);

        throw new QueryPreparationException(
            $"The entity {resultType.Name} has {ctorParams.Length} constructor parameter(s), but the projection materializes {argumentCount} value(s) after expanding the range-column pairs; map the entity to a parameterless constructor or to a constructor whose parameters match the selected columns.");
    }

    private static Expression BuildMemberInit(
        Type resultType,
        ConstructorInfo ctorInfo,
        SelectExpression[] selectList,
        Func<SelectExpression, Expression> mapColumn)
    {
        var bindings = new List<MemberBinding>(selectList.Length);
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];

            // The upper bound of a range pair is materialized together with its lower sibling.
            if (column.RangeColumnRole == RangeColumnRole.Upper)
                continue;

            var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;

            if (column.RangeColumnRole == RangeColumnRole.Lower)
            {
                var upper = FindUpper(selectList, i, column.PropertyInfo);
                bindings.Add(Expression.Bind(propInfo, BuildRangeValue(column, upper, mapColumn)));
                continue;
            }

            bindings.Add(Expression.Bind(propInfo, mapColumn(column)));
        }

        return Expression.MemberInit(Expression.New(ctorInfo), bindings);
    }

    private static SelectExpression FindUpper(SelectExpression[] selectList, int lowerIndex, PropertyInfo? owner)
    {
        for (var i = lowerIndex + 1; i < selectList.Length; i++)
        {
            if (selectList[i].RangeColumnRole == RangeColumnRole.Upper && selectList[i].PropertyInfo == owner)
                return selectList[i];
        }

        throw new QueryPreparationException($"The range-pair lower column at index {lowerIndex} has no matching upper column.");
    }

    private static Expression BuildRangeValue(SelectExpression lower, SelectExpression upper, Func<SelectExpression, Expression> mapColumn)
    {
        var ownerType = lower.PropertyInfo!.PropertyType;
        var rangeType = Nullable.GetUnderlyingType(ownerType) ?? ownerType;
        var ctor = RangeCtorCache.GetOrAdd(rangeType, static type => type.GetConstructors().Single(c => c.GetParameters().Length == 7));
        var rangeColumns = lower.RangeColumns!;

        var lowerValue = mapColumn(lower);
        var upperValue = mapColumn(upper);
        var lowerInfinite = Expression.Equal(lowerValue, Expression.Constant(null, lowerValue.Type));
        var upperInfinite = Expression.Equal(upperValue, Expression.Constant(null, upperValue.Type));

        Expression value = Expression.New(
            ctor,
            lowerValue,
            upperValue,
            Expression.Constant(rangeColumns.LowerInclusive),
            Expression.Constant(rangeColumns.UpperInclusive),
            lowerInfinite,
            upperInfinite,
            Expression.Constant(false));

        return value.Type == ownerType ? value : Expression.Convert(value, ownerType);
    }
}
