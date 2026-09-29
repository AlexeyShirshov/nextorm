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
    /// <param name="isNull">
    /// Builds the expression that tests whether one projected column reads as SQL <c>NULL</c>, used to
    /// materialize an entity item on the missing side of an outer join as <see langword="null"/>.
    /// Providers that do not read from a data record (in-memory) pass <see langword="null"/> to skip
    /// the test.
    /// </param>
    public static Expression Build(
        Type resultType,
        ParameterExpression param,
        SelectExpression[] selectList,
        bool ignoreColumns,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns = null,
        Func<SelectExpression, Expression>? isNull = null)
    {
        // A projection that contains entity-typed items (the preparer expanded each into its mapped
        // scalar columns and tagged them) is materialized by rebuilding each item as an entity and
        // assigning it to its projection slot; ordinary projections keep the existing path.
        if (!ignoreColumns)
        {
            for (var i = 0; i < selectList.Length; i++)
            {
                if (selectList[i].ProjectionItem is not null)
                    return BuildProjectionItems(resultType, param, selectList, mapColumn, mapDynamicColumns, isNull);
            }
        }

        return BuildCore(resultType, param, selectList, ignoreColumns, mapColumn, mapDynamicColumns);
    }

    /// <summary>
    /// Materializes a projection whose columns carry <see cref="SelectExpression.ProjectionItem"/>
    /// groups: columns of one group are rebuilt into the entity instance (through
    /// <see cref="BuildCore"/>, the same path a top-level entity select uses) and assigned to the
    /// group's projection slot. An entity group whose columns all read as SQL <c>NULL</c> materializes
    /// as <see langword="null"/>, which is how the missing side of an outer join round-trips.
    /// </summary>
    private static Expression BuildProjectionItems(
        Type resultType,
        ParameterExpression param,
        SelectExpression[] selectList,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns,
        Func<SelectExpression, Expression>? isNull)
    {
        // The select list may mix entity-typed projection items with ordinary columns, the
        // dynamic-columns store and range-column pairs. Group the item columns; every other column gets
        // its own value slot (a range pair collapses into one), mirroring the special-casing
        // BuildCore applies to a plain projection so a mixed projection keeps the same semantics.
        var items = new List<(ProjectionEntityItem? Item, List<SelectExpression> Columns)>();
        var groups = new Dictionary<ProjectionEntityItem, int>(ReferenceEqualityComparer.Instance);

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

            if (column.ProjectionItem is { } item)
            {
                if (!groups.TryGetValue(item, out var slot))
                {
                    slot = items.Count;
                    groups[item] = slot;
                    items.Add((item, new List<SelectExpression>()));
                }

                items[slot].Columns.Add(column);
                continue;
            }

            // The upper bound of a range pair is materialized together with its lower sibling.
            if (column.RangeColumnRole == RangeColumnRole.Upper)
                continue;

            items.Add((null, [column]));
        }

        var values = new Expression[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var (item, columns) = items[i];
            values[i] = item is not null
                ? BuildEntityItem(item, param, columns, mapColumn, mapDynamicColumns, isNull)
                : BuildNonItemValue(columns[0], selectList, mapColumn, mapDynamicColumns, dynamicColumns, starStart);
        }

        var ctorInfo = resultType.GetConstructors()
            .OrderByDescending(it => it.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new QueryPreparationException($"Cannot get ctor from {resultType}");
        var ctorParams = ctorInfo.GetParameters();

        if (ctorParams.Length == values.Length)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Type != ctorParams[i].ParameterType)
                    values[i] = Expression.Convert(values[i], ctorParams[i].ParameterType);
            }

            return Expression.New(ctorInfo, values);
        }

        // The longest constructor does not take one argument per projected item, so the projection has
        // to be assembled member by member. That requires a parameterless constructor: invoking the
        // parameterful ctor with no arguments would throw while building the tree. A shape that offers
        // neither a matching constructor nor a parameterless one is rejected with a preparation error.
        var parameterlessCtor = resultType.GetConstructor(Type.EmptyTypes)
            ?? throw new QueryPreparationException(
                $"The projection type {resultType.Name} has {ctorParams.Length} constructor parameter(s), but the projection materializes {values.Length} item(s); map it to a parameterless constructor or to a constructor whose parameters match the projected items.");

        var bindings = new List<MemberBinding>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var (item, columns) = items[i];

            // An entity item addresses a projection member when the projection is a property; a
            // constructor position has no member to bind, so member-init cannot assemble it and must not
            // guess a member from the entity's own property name.
            var member = item is not null
                ? item.Member ?? throw new QueryPreparationException(
                    $"Projection item {item.EntityType.Name} at slot {item.Slot} addresses a constructor position and cannot be bound member by member on {resultType.Name}; map {resultType.Name} to a constructor whose parameters match the projected items.")
                : ResolveNonItemMember(resultType, columns[0]);

            if (member is PropertyInfo property && values[i].Type != property.PropertyType)
                values[i] = Expression.Convert(values[i], property.PropertyType);

            bindings.Add(Expression.Bind(member, values[i]));
        }

        return Expression.MemberInit(Expression.New(parameterlessCtor), bindings);
    }

    /// <summary>
    /// Maps a non-item projected column, applying the same dynamic-columns-store and range-pair
    /// special-casing <see cref="BuildCore"/> applies to a plain projection.
    /// </summary>
    private static Expression BuildNonItemValue(
        SelectExpression column,
        SelectExpression[] selectList,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns,
        DynamicColumns? dynamicColumns,
        int starStart)
    {
        if (column.IsDynamicColumnsStore)
        {
            if (mapDynamicColumns is null)
                throw new NotSupportedException("The provider cannot materialize the dynamic-columns store.");

            return mapDynamicColumns(column, starStart, dynamicColumns!);
        }

        if (column.RangeColumnRole == RangeColumnRole.Lower)
        {
            var index = Array.IndexOf(selectList, column);
            var upper = FindUpper(selectList, index, column.PropertyInfo);
            return BuildRangeValue(column, upper, mapColumn);
        }

        return mapColumn(column);
    }

    /// <summary>Resolves the projection member a non-item column is bound to in the member-init fallback.</summary>
    private static MemberInfo ResolveNonItemMember(Type resultType, SelectExpression column)
        => (MemberInfo?)column.PropertyInfo
            ?? resultType.GetProperty(column.PropertyName!)
            ?? throw new QueryPreparationException($"Cannot bind projection item {column.PropertyName} of {resultType.Name}");

    private static Expression BuildEntityItem(
        ProjectionEntityItem item,
        ParameterExpression param,
        List<SelectExpression> columns,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns,
        Func<SelectExpression, Expression>? isNull)
    {
        var entity = BuildCore(item.EntityType, param, [.. columns], ignoreColumns: false, mapColumn, mapDynamicColumns);

        if (isNull is null)
            return entity;

        Expression? anyNotNull = null;
        for (var i = 0; i < columns.Count; i++)
        {
            var notNull = Expression.Not(isNull(columns[i]));
            anyNotNull = anyNotNull is null ? notNull : Expression.OrElse(anyNotNull, notNull);
        }

        if (anyNotNull is null)
            return entity;

        var nullValue = item.EntityType.IsValueType
            ? (Expression)Expression.Default(item.EntityType)
            : Expression.Constant(null, item.EntityType);

        return Expression.Condition(anyNotNull, entity, nullValue);
    }

    private static Expression BuildCore(
        Type resultType,
        ParameterExpression param,
        SelectExpression[] selectList,
        bool ignoreColumns,
        Func<SelectExpression, Expression> mapColumn,
        Func<SelectExpression, int, DynamicColumns, Expression>? mapDynamicColumns)
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
