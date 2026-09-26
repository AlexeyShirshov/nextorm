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
    /// <param name="resultType">The type a row is materialized into.</param>
    /// <param name="param">The row parameter the column accessors read from.</param>
    /// <param name="selectList">The projected columns, in result-set order.</param>
    /// <param name="ignoreColumns">Whether the projection is ignored (Any/Count).</param>
    /// <param name="mapColumn">Builds the accessor expression of one projected column.</param>
    /// <param name="mapDynamicColumns">
    /// Builds the accessor expression of the dynamic-columns store from its entry, the ordinal of the
    /// first star field and the reader for the mapped columns. Providers that do not support the store
    /// pass <see langword="null"/> and reject an entity that declares one.
    /// </param>
    public static Expression Build(
        Type resultType,
        ParameterExpression param,
        SelectExpression[] selectList,
        bool ignoreColumns,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns = null)
    {
        var ctorInfo = resultType.GetConstructors()
            .OrderByDescending(it => it.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new QueryPreparationException($"Cannot get ctor from {resultType}");
        var ctorParams = ctorInfo.GetParameters();

        if (ignoreColumns)
            return Expression.New(ctorInfo);

        var dynamicIndex = -1;
        for (var i = 0; i < selectList.Length; i++)
        {
            if (selectList[i].IsDynamicColumnsStore)
            {
                dynamicIndex = i;
                break;
            }
        }

        if (dynamicIndex >= 0)
        {
            if (mapDynamicColumns is null)
                throw new NotSupportedException($"The provider cannot materialize the dynamic-columns store of {resultType.Name}.");

            var parameterlessCtor = resultType.GetConstructor(Type.EmptyTypes)
                ?? throw new QueryPreparationException($"The entity {resultType.Name} declares a dynamic-columns store but has no parameterless constructor; map it to a parameterless constructor.");
            return BuildMemberInit(resultType, parameterlessCtor, selectList, mapColumn, mapDynamicColumns);
        }

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
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns = null)
    {
        var bindings = new List<MemberBinding>(selectList.Length);
        DynamicColumns? dynamicColumns = null;
        var starStart = -1;
        if (mapDynamicColumns is not null)
        {
            var names = new List<string>(selectList.Length);
            var nonStoreCount = 0;
            for (var i = 0; i < selectList.Length; i++)
            {
                var column = selectList[i];
                if (column.IsDynamicColumnsStore)
                    continue;

                nonStoreCount++;
                if (column.PropertyName is not null)
                    names.Add(column.PropertyName);
                if (column.PhysicalColumnName is not null)
                    names.Add(column.PhysicalColumnName);
            }

            dynamicColumns = new DynamicColumns(names.ToArray());
            starStart = nonStoreCount;
        }

        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];

            if (column.IsDynamicColumnsStore)
            {
                var storeProperty = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
                bindings.Add(Expression.Bind(storeProperty, mapDynamicColumns!(column, starStart, dynamicColumns!)));
                continue;
            }

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
