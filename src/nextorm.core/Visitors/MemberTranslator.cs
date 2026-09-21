using System.Linq.Expressions;
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

        return false;
    }

    internal static Expression? VisitMember(BaseExpressionVisitor visitor, MemberExpression node)
    {
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
                    var colName = node.Member.GetPropertyColumnName();
                    if (!string.IsNullOrEmpty(colName))
                    {
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

                        visitor.Builder!.Append(colName);
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

                var (n, innerQuery) = visitor.ColumnsProvider.FindQueryCommand(visitor.EntityType);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(visitor.Options);
                    var col = sqlBuilder.MakeColumn(innerCol, innerQuery.EntityType!, true, renameAware: true);
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

            visitor.Params.Add(new Parameter(node.Member.Name, ((Func<object>)del)()));

            if (!visitor.IsParamMode)
                visitor.Builder!.Append(visitor.Dialect.MakeParam(node.Member.Name));

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

                var colName = memberAccessExp.Member.GetPropertyColumnName();
                if (!string.IsNullOrEmpty(colName))
                {
                    visitor.Builder!.Append(colName);
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

                visitor.Builder!.Append(column);
            }

            return node;
        }
        else
        {
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
                    visitor.Params.Add(new Parameter(node.Member.Name, ((Func<object?, object>)del)(twoTypeVisitor.Target2!.Value)));

                    if (!visitor.IsParamMode)
                        visitor.Builder!.Append(visitor.Dialect.MakeParam(node.Member.Name));

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
                        // told which occurrence is meant. The member name ("Item1".."Item8") carries
                        // the 1-based position among all projection items; the occurrence for every
                        // position of a given projection shape is cached (it is a pure function of
                        // the generic arguments).
                        var propExp = (MemberExpression)node.Expression!;
                        var name = propExp.Member.Name;
                        var digitsStart = name.Length;
                        while (digitsStart > 0 && char.IsAsciiDigit(name[digitsStart - 1])) digitsStart--;
                        var position = 0;
                        for (var i = digitsStart; i < name.Length; i++) position = position * 10 + (name[i] - '0');
                        position--;
                        var paramIdx = ProjectionAliasCache.GetOccurrence(lambdaParameter.Type, position);
                        tableAliasForColumn = AliasResolver.GetAliasFromParam(visitor, node.Expression!.Type, paramIdx, false);
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

                    visitor.Builder!.Append(tableAliasForColumn).Append('.');

                    hasTableAliasForColumn = true;
                }

                if (!visitor.IsParamMode)
                {
                    var colName = node.Member.GetPropertyColumnName();
                    if (!string.IsNullOrEmpty(colName))
                    {
                        visitor.Builder!.Append(colName);
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

                var (idx, innerQuery) = visitor.ColumnsProvider.FindQueryCommand(node.Expression!.Type);
                if (innerQuery is not null)
                {
                    var innerCol = innerQuery.SelectList!.SingleOrDefault(col => col.PropertyName == node.Member.Name);
                    if (innerCol is null)
                        throw new BuildSqlCommandException($"Cannot find inner column {node.Member.Name}");

                    var sqlBuilder = new SqlBuilder(visitor.Options);
                    var col = sqlBuilder.MakeColumn(innerCol, innerQuery.EntityType!, true, renameAware: true);

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

        var colName = node.Member.GetPropertyColumnName();
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

            visitor.Builder!.Append(aliasVisitor.Alias).Append('.').Append(colName);
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

                if (!visitor.IsParamMode)
                {
                    visitor.Builder!.Append(sql).Append(')');
                }

            return node;
        }

        return null;
    }
}
