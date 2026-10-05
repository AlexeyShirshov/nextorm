using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Translates member access into SQL: mapped columns, projection items (<c>tN</c>), nullable
/// <c>Value</c>, <c>string.Length</c>, <c>DateTime</c> parts, closure constants and referenced
/// subquery columns. Extracted from <see cref="BaseExpressionVisitor"/>; the visitor walk and the
/// emitted SQL are unchanged.
/// </summary>
internal static class MemberTranslator
{
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MemberExpression node)
    {
        var declaringType = node.Member.DeclaringType;

        // Static DateTime.Now / DateTime.UtcNow as a SQL expression instead of an evaluated parameter.
        if (node.Expression is null && declaringType == typeof(DateTime))
        {
            if (node.Member.Name == nameof(DateTime.Now) || node.Member.Name == nameof(DateTime.UtcNow))
            {
                visitor.NeedAliasForColumn = true;
                if (!visitor.IsParamMode)
                    visitor.Builder!.Append(visitor.Dialect.MakeNow(node.Member.Name == nameof(DateTime.UtcNow)));

                return true;
            }

            return false;
        }

        if (node.Expression is null)
            return false;

        // Nullable<T>.Value is a CLR-only wrapper: the underlying expression is rendered as is.
        if (declaringType is not null
            && node.Member.Name == nameof(Nullable<int>.Value)
            && Nullable.GetUnderlyingType(declaringType) is not null)
        {
            visitor.Visit(node.Expression);
            return true;
        }

        if (declaringType == typeof(string) && node.Member.Name == nameof(string.Length))
        {
            if (visitor.IsParamMode)
            {
                visitor.Visit(node.Expression);
                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(visitor.Dialect.MakeStringLength(visitor.VisitToString(node.Expression)));
            return true;
        }

        if (declaringType == typeof(DateTime))
        {
            var part = node.Member.Name switch
            {
                nameof(DateTime.Year) => "year",
                nameof(DateTime.Month) => "month",
                nameof(DateTime.Day) => "day",
                nameof(DateTime.Hour) => "hour",
                nameof(DateTime.Minute) => "minute",
                nameof(DateTime.Second) => "second",
                nameof(DateTime.DayOfYear) => "doy",
                _ => null
            };

            if (part is null)
                return false;

            if (visitor.IsParamMode)
            {
                visitor.Visit(node.Expression);
                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(visitor.Dialect.MakeDatePart(part, visitor.VisitToString(node.Expression)));
            return true;
        }

        if (TupleSqlTranslator.TryTranslateElement(visitor, node))
            return true;

        return false;
    }

    // Resolves the effective duration storage unit of a member access mapped as an entity column:
    // null for a native duration (or a non-duration member), otherwise the declared unit (ticks by
    // default). Used so a TimeSpan parameter compared with a duration column is converted to the
    // stored integer form.
    internal static DurationUnit? ResolveDurationUnit(BaseExpressionVisitor visitor, Expression expression)
    {
        if (expression is MemberExpression { Member: PropertyInfo pi }
            && FindProperty(visitor.EntityType, pi) is { } property)
            return DurationStorage.ResolveStorageUnit(property, visitor.Dialect);

        return null;
    }

    // Indexed lookup of a mapped property: a query is prepared once per plan, but the lookups run
    // per projected column and per comparison operand, so the property index built by EntityMetadata
    // keeps this O(1). External IEntityMetadata implementations fall back to the linear scan.
    internal static IPropertyMetadata? FindProperty(IEntityMetadata metadata, PropertyInfo property)
    {
        if (metadata is EntityMetadata indexed)
            return indexed.FindProperty(property);

        var properties = metadata.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].PropertyInfo == property)
                return properties[i];
        }

        return null;
    }

    // Resolves the mapped property of an entity type, or null when the type is not mapped or the
    // member is not one of its columns.
    internal static IPropertyMetadata? FindProperty(Type? entityType, PropertyInfo property)
        => entityType is not null && DataContextCache.Metadata.TryGetValue(entityType, out var metadata)
            ? FindProperty(metadata, property)
            : null;

    // Resolves the mapped property of a member access. The member's declaring type is consulted
    // first (a projection over a join addresses the joined entity), then the caller's fallback
    // metadata (the visitor's entity type, or the projection source type). Null when the member is
    // not mapped by either.
    internal static IPropertyMetadata? ResolveProperty(PropertyInfo property, IEntityMetadata? fallbackMetadata)
    {
        if (property.DeclaringType is not null && DataContextCache.Metadata.TryGetValue(property.DeclaringType, out var byDeclaring))
            return FindProperty(byDeclaring, property);

        return fallbackMetadata is null ? null : FindProperty(fallbackMetadata, property);
    }

    // Resolves the value converter of an entity member access, if the mapped property declares one.
    // Used so a constant compared with a converted column is bound as the provider representation, and
    // the same converter is applied to the elements of an IN/Contains value list. The metadata is keyed
    // by the member's declaring type (the entity type in a simple query, or the joined entity type in a
    // projection), falling back to the visitor's entity type. A boxed comparison operand wraps the
    // member in a Convert (the C# lowering of an enum comparison), so the wrapper is unwrapped first.
    internal readonly record struct ResolvedConverter(IPropertyValueConverter Converter, Type ModelType);

    internal static ResolvedConverter? ResolveConverter(BaseExpressionVisitor visitor, Expression expression)
    {
        expression = TypeFacts.UnwrapConvert(expression);
        if (expression is not MemberExpression { Member: PropertyInfo pi })
            return null;

        IPropertyMetadata? property;
        if (pi.DeclaringType is not null && DataContextCache.Metadata.TryGetValue(pi.DeclaringType, out var byDeclaring))
            property = FindProperty(byDeclaring, pi);
        else
            property = FindProperty(visitor.EntityType, pi);

        if (property?.Converter is not { } converter)
            return null;

        var resolved = converter is IJsonColumnConverter json ? json.Resolve(visitor.Dialect) : converter;
        return new ResolvedConverter(resolved, Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType);
    }

    internal static string? ResolveCollation(BaseExpressionVisitor visitor, Type entityType, MemberInfo member)
    {
        if (member is not PropertyInfo pi)
            return null;

        return FindProperty(entityType, pi)?.Collation is { Length: > 0 } collation ? collation : null;
    }

    private static string? ResolveRangeColumnsMember(BaseExpressionVisitor visitor, MemberInfo member)
    {
        if (member is not PropertyInfo pi || visitor.EntityType is null)
            return null;

        var rangeColumns = FindProperty(visitor.EntityType, pi)?.RangeColumns;
        if (rangeColumns is null)
            return null;

        return visitor.RangeColumnRole switch
        {
            RangeColumnRole.Lower => rangeColumns.LowerColumn,
            RangeColumnRole.Upper => rangeColumns.UpperColumn,
            _ => throw new NotSupportedException($"The property '{pi.Name}' is stored as a pair of columns and cannot be used as a single value; use a range function or operator over it.")
        };
    }

    internal static void AppendColumn(BaseExpressionVisitor visitor, string colName, string? collation)
    {
        if (string.IsNullOrEmpty(collation) || visitor.SuppressColumnCollation)
        {
            visitor.AppendIdentifier(colName);
            return;
        }

        if (!visitor.Dialect.SupportsCollation)
            throw new NotSupportedException($"The collation '{collation}' declared on the column cannot be expressed by this provider; it has no COLLATE clause.");

        var builder = visitor.Builder!;
        var start = builder.Length;
        visitor.AppendIdentifier(colName);
        var rendered = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        builder.Append(visitor.Dialect.MakeCollate(rendered, collation, visitor.KeywordCase));
        visitor.NeedAliasForColumn = true;
    }

    internal static Expression? VisitMember(BaseExpressionVisitor visitor, MemberExpression node)    {
        if (TryTranslate(visitor, node))
            return node;

        // The element of a bound ARRAY JOIN (ArrayJoinProjection<TEntity, TElement>.Element) is the
        // value expanded by the clause; it is exposed under a generated alias, not as a physical
        // column of the source entity.
        if (node.Expression is ParameterExpression elementParam
            && typeof(IArrayJoinProjection).IsAssignableFrom(elementParam.Type)
            && node.Member.Name == ArrayJoinNames.ElementMember)
        {
            if (!visitor.IsParamMode)
            {
                visitor.Builder!.Append(ArrayJoinNames.ElementAlias);
                visitor.NeedAliasForColumn = true;
                visitor.ColumnName = null;
            }

            return node;
        }

        if (node.Expression?.Type == visitor.EntityType)
        {
            if (node.Expression!.Type!.IsAssignableTo(typeof(IProjection)))
            {
                if (!visitor.IsParamMode)
                    visitor.Builder!.Append(node.Member.Name).Append('.');

                return node;
            }
            else
            {
                if (!visitor.IsParamMode)
                {
                    var colName = ResolveRangeColumnsMember(visitor, node.Member)
                        ?? node.Member.GetPropertyColumnName(visitor.Options.NamingConvention);
                    if (!string.IsNullOrEmpty(colName))
                    {
                        if (TryTranslateDerivedProjectionMember(visitor, node, hasTableAliasForColumn: false))
                            return node;

                        if (!visitor.DontNeedAlias && visitor.ColumnsProvider.HasAliases)
                        {
                            string? tableAliasForColumn = null;
                            var v = new TypeExpressionVisitor<ParameterExpression>();
                            v.Visit(node.Expression);
                            if (v.Has)
                            {
                                // var aliasVisitor = new AliasFromProjectionVisitor();
                                // aliasVisitor.Visit(node.Expression);
                                // tableAliasForColumn = aliasVisitor.Alias;

                                // if (string.IsNullOrEmpty(tableAliasForColumn))
                                tableAliasForColumn = AliasResolver.GetAliasFromParam(visitor, v.Target!, false);
                            }

                            if (!string.IsNullOrEmpty(tableAliasForColumn))
                                visitor.Builder!.Append(tableAliasForColumn).Append('.');
                        }

                        AppendColumn(visitor, colName, ResolveCollation(visitor, visitor.EntityType, node.Member));
                        visitor.ColumnName = colName;
                        return node;
                    }
                    //var colAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
                    // if (colAttr is not null)
                    // {
                    //     visitor.Builder!.Append(colAttr.Name);
                    //     visitor.ColumnName = colAttr.Name;
                    //     return node;
                    // }
                }

                var (n, innerQuery) = visitor.ColumnsProvider.FindQueryCommand(visitor.EntityType, visitor.IncludeNestedSources);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var col = MakeInnerColumn(visitor, innerQuery, innerCol);
                    if (!visitor.IsParamMode)
                    {
                        if (!visitor.DontNeedAlias)
                            visitor.Builder!.Append(visitor.AliasProvider!.FindAlias(n)).Append('.');

                        if (col.NeedAliasForColumn)
                            visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(innerCol.PropertyName!));
                        else
                        {
                            visitor.Builder!.Append(col.Column);
                            //visitor.ColumnName = col.Name;
                        }
                    }
                }
            }
        }
        else if (node.Expression is null)
        {
            if (!visitor.IsParamMode && node.Member.DeclaringType == typeof(string))
            {
                if (node.Member.Name == nameof(string.Empty))
                    visitor.Builder!.Append(visitor.Dialect.EmptyString);

                return node;
            }

            var key = new ExpressionKey(node, visitor.QueryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(node, typeof(object));
                del = Expression.Lambda<Func<object>>(body).Compile();

                DataContextCache.ExpressionsCache[key] = del;

                if (visitor.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    visitor.Logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object>)del)());
                }
                else if (visitor.Logger?.IsEnabled(LogLevel.Debug) ?? false) visitor.Logger.LogDebug("Expression cache miss on visit where");
            }

            var parameterName = visitor.Options.ParameterNamePrefix + node.Member.Name;
            visitor.TryAddCapturedParameter(parameterName, ((Func<object>)del)(), node);

            if (!visitor.IsParamMode)
                visitor.Builder!.Append(visitor.Dialect.MakeParam(parameterName));

            return node;
        }
        else if (node.Expression is NewExpression n)
        {
            if (!visitor.IsParamMode && n.Type.IsGenericType && n.Type.GetGenericTypeDefinition() == typeof(OuterRefMarker<>) && n.Arguments is [ConstantExpression cexp] && cexp.Value is int idx)
            {
                var memberAccessExp = (MemberExpression)visitor.QueryProvider.OuterReferences![idx];
                string? tableAliasForColumn = null;

                if (memberAccessExp!.Type!.TryGetProjectionDimension(out _))
                {
                    var aliasVisitor = new AliasFromProjectionVisitor();
                    aliasVisitor.Visit(node.Expression);
                    tableAliasForColumn = aliasVisitor.Alias;
                }
                else
                    tableAliasForColumn = AliasResolver.GetOuterAliasFromParam(visitor, (ParameterExpression)memberAccessExp.Expression!, false);

                visitor.Builder!.Append(tableAliasForColumn).Append('.');

                var colName = memberAccessExp.Member.GetPropertyColumnName(visitor.Options.NamingConvention);
                if (!string.IsNullOrEmpty(colName))
                {
                        visitor.AppendIdentifier(colName);
                        visitor.ColumnName = colName;
                        return node;
                }
            }
        }
        else if (TryTranslateProjectionOuterReference(visitor, node))
        {
            return node;
        }
        else if (node.Expression.Type == typeof(TableColumn))
        {
            if (!visitor.IsParamMode && node.Expression is MethodCallExpression mce && mce.Arguments is [ConstantExpression arg] && arg.Value is string column)
            {
                string? tableAliasForColumn = null;
                var v = new TypeExpressionVisitor<ParameterExpression>();
                v.Visit(mce.Object);
                if (v.Has)
                {
                    var aliasVisitor = new AliasFromProjectionVisitor();
                    aliasVisitor.Visit(node.Expression);
                    tableAliasForColumn = aliasVisitor.Alias;

                    if (string.IsNullOrEmpty(tableAliasForColumn))
                        tableAliasForColumn = AliasResolver.GetAliasFromParam(visitor, v.Target!, false);
                }

                // Only a resolved table alias is prefixed: without a join there is a single source and
                // no alias, so emitting "<empty>." would produce invalid SQL (".column").
                if (!string.IsNullOrEmpty(tableAliasForColumn))
                    visitor.Builder!.Append(tableAliasForColumn).Append('.');

                visitor.AppendIdentifier(column);
            }

            return node;
        }
        else
        {
            // A query filter imported from EF Core reads its live owner as
            // (TContext)<owner-getter>(QueryFilterContext.Context). Parameterize the whole
            // owner-getter/member subtree with a host-free accessor invoked with the executing context,
            // so the owner instance and the value it produces never enter a process-wide key. A native
            // context filter (IDataContext.Properties reads) has no owner-getter and keeps its path.
            if (QueryFilterContextAccessor.TryTranslate(visitor, node))
                return node;

            // The common member access is <param>.Member (join condition) or <param>.tN.Member
            // (projection), so the lambda parameter can be read structurally. Only the remaining
            // shapes (closure constants, deeper chains) need the allocating twoTypeVisitor.
            ParameterExpression? lambdaParameter = node.Expression switch
            {
                ParameterExpression p => p,
                MemberExpression { Expression: ParameterExpression p } => p,
                _ => null
            };

            if (lambdaParameter is null)
            {
                var twoTypeVisitor = new TypeExpressionVisitor<ParameterExpression, ConstantExpression>();
                twoTypeVisitor.Visit(node.Expression);

                //if (node.Expression is ConstantExpression ce)
                if (!twoTypeVisitor.Has1 && twoTypeVisitor.Has2)
                {
                    //var ce = twoTypeVisitor.Target2!;
                    // Note: expression caching is currently unconditional (the CacheExpressions flag is not wired).
                    var key = new ExpressionKey(node, visitor.QueryProvider);
                    Delegate? del = null;
                    DataContextCache.ExpressionsCache.TryGetValue(key, out del);

                    if (del is null)
                    {
                        var p = Expression.Parameter(typeof(object));
                        var replace = new ReplaceConstantExpressionVisitor(Expression.Convert(p, twoTypeVisitor.Target2!.Type));
                        var body = Expression.Convert(replace.Visit(node), typeof(object));
                        del = Expression.Lambda<Func<object?, object>>(body, p).Compile();

                        DataContextCache.ExpressionsCache[key] = del;

                        if (visitor.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                        {
                            visitor.Logger.LogTrace("Expression cache miss on visit where. hashcode: {hash}, value: {value}", key.GetHashCode(), ((Func<object?, object>)del)(twoTypeVisitor.Target2.Value));
                        }
                        else if (visitor.Logger?.IsEnabled(LogLevel.Debug) ?? false) visitor.Logger.LogDebug("Expression cache miss on visit where");
                    }
                    // var value = 1;
                    var parameterName = visitor.Options.ParameterNamePrefix + node.Member.Name;
                    visitor.TryAddCapturedParameter(parameterName, ((Func<object?, object>)del)(twoTypeVisitor.Target2!.Value), node);

                    if (!visitor.IsParamMode)
                        visitor.Builder!.Append(visitor.Dialect.MakeParam(parameterName));

                    return node;
                }

                lambdaParameter = twoTypeVisitor.Has1 ? twoTypeVisitor.Target1 : null;
            }

            if (lambdaParameter is not null)
            {
                var hasTableAliasForColumn = false;
                string? tableAliasForColumn = null;

                // Aliases are only needed to emit SQL text. In parameter-extraction mode (visitor.IsParamMode)
                // nothing is appended, and visitor.ColumnsProvider is not populated by MakeFrom/MakeJoin in
                // that mode, so resolving the alias would fail for join queries.
                if (!visitor.IsParamMode && !visitor.DontNeedAlias)
                {
                    if (lambdaParameter.Type!.IsAssignableTo(typeof(IProjection)))
                    {
                        // When a type repeats inside the projection, the columns provider has to be
                        // told which occurrence is meant. The member carries its 1-based slot: the
                        // digits of "Item1".."Item8" for engine projections, or JoinSlotAttribute for
                        // a generated alias member ("Buyer"/"Approver"). The occurrence for every
                        // position of a given projection shape is cached (it is a pure function of
                        // the generic arguments).
                        var propExp = (MemberExpression)node.Expression!;
                        var position = ProjectionAliasCache.GetMemberPosition(propExp.Member);
                        var paramIdx = ProjectionAliasCache.GetOccurrence(lambdaParameter.Type, position);

                        var sourceIdx = ResolveProjectionItemAliasIndex(
                            visitor.ColumnsProvider,
                            node.Expression.Type,
                            paramIdx,
                            lambdaParameter,
                            visitor.IncludeNestedSources);

                        tableAliasForColumn = visitor.AliasProvider!.FindAlias(sourceIdx);
                    }
                    else if (visitor.Dim >= 2)
                    {
                        // In a chained join (dim >= 2) the condition is
                        // (accumulated projection, joinedEntity). Only the joined entity is a
                        // non-projection parameter, and its table alias is the (dim + 1)-th one.
                        // Resolving by type alone would pick the first table of that type, which is
                        // wrong when the same entity type is joined again.
                        tableAliasForColumn = visitor.AliasProvider!.FindAlias(visitor.Dim);
                    }
                    else
                        tableAliasForColumn = AliasResolver.GetAliasFromParam(visitor, lambdaParameter, false);

                    if (tableAliasForColumn is not null)
                    {
                        visitor.Builder!.Append(tableAliasForColumn).Append('.');
                        hasTableAliasForColumn = true;
                    }
                }

                if (!visitor.IsParamMode)
                {
                    // A projection-item access (p.ItemN.Member) over a derived/read-CTE source resolves
                    // through the source's slot-tagged shape. A metadata-less shape item has no mapped
                    // column name, so the mapped-name branch below would skip the shape lookup and fail;
                    // try the derived shape first for a projection-item access.
                    if (lambdaParameter.Type!.IsAssignableTo(typeof(IProjection))
                        && node.Expression is MemberExpression
                        && TryTranslateDerivedProjectionMember(visitor, node, hasTableAliasForColumn))
                        return node;

                    var colName = ResolveRangeColumnsMember(visitor, node.Member)
                        ?? node.Member.GetPropertyColumnName(visitor.Options.NamingConvention);
                    if (!string.IsNullOrEmpty(colName))
                    {
                        if (TryTranslateDerivedProjectionMember(visitor, node, hasTableAliasForColumn))
                            return node;

                        var collation = lambdaParameter.Type!.IsAssignableTo(typeof(IProjection))
                            ? null
                            : ResolveCollation(visitor, lambdaParameter.Type, node.Member);
                        AppendColumn(visitor, colName, collation);
                        visitor.ColumnName = colName;
                        return node;
                    }
                    // var colAttr = node.Member.GetCustomAttribute<ColumnAttribute>();
                    // if (colAttr is not null)
                    // {
                    //     visitor.Builder!.Append(colAttr.Name);
                    //     visitor.ColumnName = colAttr.Name;
                    //     return node;
                    // }
                }

                var (idx, innerQuery) = visitor.ColumnsProvider.FindQueryCommand(node.Expression!.Type, visitor.IncludeNestedSources);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var col = MakeInnerColumn(visitor, innerQuery, innerCol);

                    if (!visitor.IsParamMode)
                    {
                        if (!hasTableAliasForColumn)
                            visitor.Builder!.Append(visitor.AliasProvider!.FindAlias(idx)).Append('.');

                        if (col.NeedAliasForColumn)
                            visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(innerCol.PropertyName!));
                        else
                            visitor.Builder!.Append(col.Column);
                    }
                }
                else if (!visitor.IsParamMode)
                {
                    // The member is neither mapped in the entity metadata nor resolvable through an
                    // inner query, so no column name can be produced for it.
                    throw new BuildSqlCommandException($"Cannot resolve column for member {node.Member.Name}");
                }
                //}
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a member of a source whose actual <c>FROM</c> is a derived table rather than a physical
    /// table. A derived table exposes its projection under the projected (property) names, so a member
    /// access must reference the exposed name, not the physical column name. The lookup is confined to
    /// the command currently being rendered: an out-of-scope (already popped) derived source must not
    /// change how a physical column of the current scope is rendered.
    /// </summary>
    private static bool TryTranslateDerivedProjectionMember(
        BaseExpressionVisitor visitor,
        MemberExpression node,
        bool hasTableAliasForColumn)
    {
        (int Index, QueryCommand? Command)? found;
        var slot = -1;
        var isProjectionItem = false;

        // An identity join/CTE item member (p.ItemN.Property) resolves its source
        // command through the projection parameter and its column by slot + property name, so a
        // self-join of one type does not collapse the two items onto the first matching column.
        if (node.Expression is MemberExpression itemAccess
            && itemAccess.Expression is ParameterExpression projectionParameter
            && projectionParameter.Type.IsAssignableTo(typeof(IProjection)))
        {
            isProjectionItem = true;
            slot = ProjectionAliasCache.GetMemberPosition(itemAccess.Member);
            found = visitor.ColumnsProvider.FindInScopeQueryCommand(projectionParameter, fromProjection: false);

            // The projection-parameter lookup only matches an identity CTE shape, which exposes
            // slot-tagged columns. A plain derived query is registered by its item type instead, so
            // fall back to the type-based lookup (and drop the slot) when the parameter lookup misses
            // or its columns are not slot-tagged. Without this, an ordinary p.ItemN.Member reference
            // over a derived source loses its projected-name translation and emits the physical column.
            if (found is not { Command.SelectList: { } projectionColumns }
                || !projectionColumns.Any(col => col.ProjectionItem?.Slot == slot && col.PropertyName == node.Member.Name))
            {
                found = visitor.ColumnsProvider.FindInScopeQueryCommand(node.Expression!.Type);
                slot = -1;
            }
        }
        else
        {
            found = node.Expression is ParameterExpression parameter
                ? visitor.ColumnsProvider.FindInScopeQueryCommand(parameter, fromProjection: false)
                : visitor.ColumnsProvider.FindInScopeQueryCommand(node.Expression!.Type);
        }

        if (found is not { Command: { } innerQuery } || innerQuery.SelectList is null)
            return false;

        SelectExpression? innerCol = null;
        if (slot >= 0)
        {
            foreach (var candidate in innerQuery.SelectList)
            {
                if (candidate.ProjectionItem?.Slot == slot && candidate.PropertyName == node.Member.Name)
                {
                    innerCol = candidate;
                    break;
                }
            }
        }
        else
        {
            innerCol = innerQuery.SelectList.SingleOrDefault(col => col.PropertyName == node.Member.Name);
        }

        if (innerCol is null)
        {
            // A projection-item access (p.ItemN.Member) over a derived/read shape that does not expose
            // the member is unresolvable. Fail closed instead of letting the caller fall back to a
            // physical column the derived source lacks; the identity mutation RETURNING converts this
            // exception into a slot/member diagnostic before any database execution.
            if (isProjectionItem)
                throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

            return false;
        }

        // An identity shape column carries its stored per-slot alias; reference it
        // directly instead of re-deriving a name from the item's expression tree.
        if (slot >= 0
            && innerCol.PhysicalColumnName is { Length: > 0 } storedAlias
            && storedAlias.StartsWith("__s", StringComparison.Ordinal))
        {
            if (!visitor.IsParamMode)
            {
                if (!hasTableAliasForColumn && !visitor.DontNeedAlias)
                    visitor.Builder!.Append(visitor.AliasProvider!.FindAlias(found.Value.Index)).Append('.');

                visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(storedAlias));
            }

            return true;
        }

        visitor.ColumnsProvider.PushSourceScope();
        string column;
        bool needAliasForColumn;
        try
        {
            var col = MakeInnerColumn(visitor, innerQuery, innerCol);
            column = col.Column;
            needAliasForColumn = col.NeedAliasForColumn;
        }
        finally
        {
            visitor.ColumnsProvider.PopSourceScope();
        }

        if (!visitor.IsParamMode)
        {
            if (!hasTableAliasForColumn && !visitor.DontNeedAlias)
                visitor.Builder!.Append(visitor.AliasProvider!.FindAlias(found.Value.Index)).Append('.');

            if (needAliasForColumn)
                visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(innerCol.PropertyName!));
            else
                visitor.Builder!.Append(column);
        }

        return true;
    }

    /// <summary>
    /// Resolves the SQL text of a column exposed by an inner command. A typed-CTE read registers its
    /// defining command as the column shape; that command's <see cref="QueryCommand.EntityType"/> is
    /// its projection shape (not a mapped entity) and its select-list expressions still reference the
    /// CTE's own input, so rendering them against the consumer re-enters this translator on the same
    /// projection type. A typed CTE declaration body aliases every projected column to its property
    /// name, so the consumer must reference that exact output identifier and let the caller qualify it
    /// with the source alias.
    /// </summary>
    private static (bool NeedAliasForColumn, string Column) MakeInnerColumn(
        BaseExpressionVisitor visitor,
        QueryCommand innerQuery,
        SelectExpression innerCol)
    {
        if (innerQuery.From?.ColumnShape is not null || IsTypedCteProducer(visitor, innerQuery))
            return (true, innerCol.PropertyName!);

        var sqlBuilder = new SqlBuilder(visitor.Options with { IncludeNestedSources = true });
        return sqlBuilder.MakeColumn(innerCol, innerQuery.EntityType!, true, renameAware: true);
    }

    /// <summary>
    /// Renders a bare lambda parameter that is itself the value of a single-column projection source,
    /// for example a scalar typed CTE read (<c>From(scalarCte).Select(n =&gt; n + 1)</c>) or the scalar
    /// step of a recursive typed CTE (<c>Where(n =&gt; n &lt; 5)</c>). Such a source exposes no member
    /// to resolve, so the whole parameter denotes the source's one readable output column; without this
    /// the parameter would emit no SQL and produce a malformed expression (<c>( + 1)</c>). A multi-column
    /// or non-projection source is left to the default parameter handling.
    /// </summary>
    /// <returns><see langword="true"/> when the parameter was rendered as the scalar source's column.</returns>
    internal static bool TryVisitScalarSourceParameter(BaseExpressionVisitor visitor, ParameterExpression node)
    {
        if (node.Type != visitor.EntityType)
            return false;

        var (index, shape) = visitor.ColumnsProvider.FindQueryCommand(visitor.EntityType, visitor.IncludeNestedSources);

        // Only a projection source (a command registered as a column shape) with exactly one readable
        // column whose type is the row type itself can stand for the bare parameter.
        if (shape?.SelectList is not { Length: 1 } || shape.SelectList[0].PropertyType != node.Type)
            return false;

        var name = shape.SelectList[0].PropertyName ?? shape.SelectList[0].OutputName;
        if (string.IsNullOrEmpty(name))
            return false;

        // Parameter mode only walks the tree without emitting text.
        if (visitor.IsParamMode)
            return true;

        if (!visitor.DontNeedAlias && visitor.ColumnsProvider.HasAliases && visitor.AliasProvider is { } aliases)
            visitor.Builder!.Append(aliases.FindAlias(index)).Append('.');

        visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(name));
        visitor.ColumnName = name;
        return true;
    }

    /// <summary>
    /// True when <paramref name="innerQuery"/> is the defining body of a typed CTE (<c>AsCte</c>)
    /// declared on the command currently being rendered. Such a body exposes its columns under the
    /// projection's property names (see <see cref="SqlBuildContext.ExactProjectionAliases"/>), so a
    /// member read over it must reference the property name rather than the physical mapped column.
    /// The check is by reference against the command's own declaration set, so a body-to-body read
    /// (outer typed CTE over an inner one) resolves the inner producer the same way the final consumer
    /// does, while an ordinary derived subquery or a legacy <c>With</c> declaration is untouched.
    /// </summary>
    private static bool IsTypedCteProducer(BaseExpressionVisitor visitor, QueryCommand innerQuery)
    {
        // The anchor of a typed recursive CTE is not reachable through the owner's declaration set while
        // the body itself is being rendered (the renderer wraps the body, not the consumer, so its
        // QueryProvider carries no Ctes). The marker set by Cte<TResult>.CreateRecursive identifies it.
        if (innerQuery.TypedRecursiveAnchor is not null)
            return true;

        if (visitor.QueryProvider is not QueryCommand owner || owner.Ctes is not { Count: > 0 } definitions)
            return false;

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!definition.TypedProjection)
                continue;

            // A recursive definition's body is anchor UNION ALL step, so a member read of the self-reference
            // resolves against the anchor command, not the body stored in Query. Binding the anchor shape
            // here makes the step reference the CTE's declared output alias (the anchor's property name)
            // instead of re-rendering the anchor body — which for a constant/non-column anchor would inline
            // a literal (`1`) and produce an unqualified, unaddressable column such as `t1.1`.
            if (ReferenceEquals(definition.Query, innerQuery)
                || ReferenceEquals(definition.RecursiveReference?.AnchorShape, innerQuery))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the alias index of a projection-item member access (<c>p.ItemN.Member</c>). The item
    /// type is the primary lookup. Over a derived/CTE source the item type may not be a registered
    /// source while the projection shape is; only the identity (whole-projection) joined-returning shape
    /// registers that way, so the projection-parameter fallback is gated on the source command carrying
    /// <see cref="QueryCommand.IdentitySlotAliases"/>. Every other miss fails closed with the same
    /// <see cref="InvalidOperationException"/> the previous <c>AliasResolver.GetAliasFromParam</c> call
    /// produced, rather than silently emitting an unqualified column.
    /// </summary>
    internal static int ResolveProjectionItemAliasIndex(
        IColumnsProvider columnsProvider,
        Type itemType,
        int? itemOccurrence,
        ParameterExpression projectionParameter,
        bool includeNestedSources)
    {
        var sourceIdx = columnsProvider.FindAlias(itemType, itemOccurrence, false, includeNestedSources);
        if (sourceIdx is null)
        {
            var projectionSource = columnsProvider.FindQueryCommand(projectionParameter.Type, includeNestedSources);
            if (projectionSource.Item2?.IdentitySlotAliases == true)
                sourceIdx = columnsProvider.FindAlias(projectionParameter, false, includeOuterScopes: false, includeNestedSources: includeNestedSources);
        }

        return sourceIdx ?? throw new InvalidOperationException();
    }

    /// <summary>
    /// Translates a column of a join-projection outer reference
    /// (<c>OuterRefMarker&lt;T&gt;(idx).Ref.Member</c>) into <c>alias.column</c>. The marker's
    /// <c>Ref</c> is only written when a row is materialized, so it cannot be evaluated while SQL is
    /// built; the accessed member supplies the column and the enclosing projection item (stored as the
    /// outer reference, for example <c>p.Item1</c>) supplies the table alias. The plain, non-projection
    /// outer reference is handled by the <see cref="System.Linq.Expressions.ExpressionType.New"/>
    /// branch above.
    /// </summary>
    private static bool TryTranslateProjectionOuterReference(BaseExpressionVisitor visitor, MemberExpression node)
    {
        if (node.Expression is not MemberExpression
            {
                Member.Name: nameof(OuterRefMarker<int>.Ref),
                Expression: NewExpression { Type: { IsGenericType: true } markerType } marker
            }
            || markerType.GetGenericTypeDefinition() != typeof(OuterRefMarker<>)
            || marker.Arguments is not [ConstantExpression { Value: int idx }])
            return false;

        var colName = node.Member.GetPropertyColumnName(visitor.Options.NamingConvention);
        if (string.IsNullOrEmpty(colName))
            return false;

        if (visitor.QueryProvider.OuterReferences is not { } outerReferences
            || idx < 0 || idx >= outerReferences.Count)
            return false;

        if (outerReferences[idx] is not MemberExpression { Expression: ParameterExpression projectionParam } stored
            || !projectionParam.Type.TryGetProjectionDimension(out _))
            return false;

        if (!visitor.IsParamMode)
        {
            var aliasVisitor = new AliasFromProjectionVisitor();
            aliasVisitor.Visit(stored);
            if (string.IsNullOrEmpty(aliasVisitor.Alias))
                return false;

            visitor.Builder!.Append(aliasVisitor.Alias).Append('.');
            visitor.AppendIdentifier(colName);
        }

        visitor.ColumnName = colName;
        return true;
    }

    internal static Expression? VisitIndex(BaseExpressionVisitor visitor, IndexExpression node)
    {
        if (node.Type.IsAssignableTo(typeof(QueryCommand)) && node.Arguments is [ConstantExpression ce] && ce.Value is int idx)
        {
            if (!visitor.IsParamMode)
            {
                visitor.Builder!.Append('(');
            }
            visitor.NeedAliasForColumn = true;
            var innerQuery = visitor.QueryProvider.ReferencedQueries[idx];

            // Single/SingleOrDefault require at most one row. Where the engine enforces scalar-subquery
            // cardinality the extra row is rejected by the database (the command is rendered with
            // limit 2); where it does not (SQLite) a numeric projection carries a count guard
            // (QueryPreparer.WrapSingleScalarCardinalityGuard) and any other projection is refused
            // instead of silently returning the first row.
            if (innerQuery.SingleScalar && !visitor.Dialect.EnforcesScalarSubqueryCardinality
                && (innerQuery.ResultType is not { } resultType || !QueryCommand.IsCardinalityGuardable(resultType)))
                throw new NotSupportedException(
                    $"'{visitor.Dialect.GetType().Name}' cannot enforce Single/SingleOrDefault in a scalar subquery " +
                    "(a scalar subquery returning several rows yields the first row instead of an error); " +
                    "use First/FirstOrDefault or move the check to the application.");

            var sqlBuilder = new SqlBuilder(visitor.Options);
            var sql = sqlBuilder.MakeSelect(innerQuery);

            // #148-B r3: ClickHouse decorrelates a correlated scalar subquery by a join, so an empty
            // match yields SQL NULL instead of the aggregate's identity (an empty count would be 0).
            // Coalesce a navigation count back to 0 before materialization; a dialect that does not
            // report WrapsCountResult (every other provider) always returns a non-null count.
            if (innerQuery.IsWideNavigationCount && visitor.Dialect.WrapsCountResult)
                sql = visitor.Dialect.MakeCoalesce($"({sql})", "0");

                if (!visitor.IsParamMode)
                {
                    visitor.Builder!.Append(sql).Append(')');
                }

            return node;
        }

        return null;
    }
}
