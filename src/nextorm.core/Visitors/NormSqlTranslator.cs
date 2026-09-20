using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Translates the <see cref="SqlFunctions"/> helper (<c>SqlFunctions.Parameter</c>) and the <see cref="CommonFunctions"/>
/// built-ins (aggregates, <c>EXISTS</c>/<c>ANY</c>/<c>ALL</c> and <c>IN</c> subqueries, value-list
/// <c>IN</c>) into SQL. Extracted from <see cref="BaseExpressionVisitor.VisitMethodCall"/>; the
/// branch order, the visitor walk and therefore the parameter numbering are unchanged.
/// </summary>
internal static class NormSqlTranslator
{
    /// <summary>
    /// Returns <c>false</c> for a call whose declaring type is neither <see cref="SqlFunctions"/> nor
    /// <see cref="CommonFunctions"/>; otherwise translates it (or throws, like the original branches)
    /// and returns <c>true</c>.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(SqlFunctions))
        {
            TranslateNormParam(visitor, node);
            return true;
        }

        // PG_SQL derives from CommonFunctions, so IsAssignableFrom covers both surfaces.
        if (typeof(CommonFunctions).IsAssignableFrom(node.Method.DeclaringType))
        {
            TranslateNormSql(visitor, node);
            return true;
        }

        return false;
    }

    /// <summary>Emits <c>SqlFunctions.Parameter</c> as a named parameter; any other <see cref="SqlFunctions"/> method throws.</summary>
    private static void TranslateNormParam(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var paramIdx = node switch
        {
            {
                Method.Name: nameof(SqlFunctions.Parameter),
                Arguments: [ConstantExpression constExp]
            } => constExp.Value is int i ? i : -1,
            _ => -1
        };

        if (paramIdx >= 0)
        {
            // Reuse the cached norm_pN table instead of string.Format (same names, no boxing
            // and no composite-formatting pass on the SQL-build path).
            var paramName = NormParam.GetName(paramIdx);
            visitor.Params.Add(new Parameter(paramName, null));
            if (!visitor.IsParamMode)
                visitor.Builder!.Append(visitor.Dialect.MakeParam(paramName));

            return;
        }
        else
            throw new NotSupportedException(node.Method.Name);
    }

    /// <summary>Emits the <see cref="CommonFunctions"/> built-ins; an unrecognised method throws.</summary>
    private static void TranslateNormSql(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        // A window function is only valid once it has been given a specification; emitting the bare
        // call would produce invalid SQL, so fail with an actionable message instead. The declaring-type
        // guard keeps the PostgreSQL ordered-set aggregate percentile_cont (declared on
        // PostgresFunctions) distinct from the CommonFunctions window percentile (same method name).
        if (node.Method.DeclaringType == typeof(CommonFunctions)
            && WindowSql.MapWindowFunctionName(node.Method.Name) is { } windowFunction)
            throw new NotSupportedException($"The window function {windowFunction} must be completed with Over(...).");

        // The array overloads of any/all (column = any(@array)); the subquery overload is handled below.
        if (ArraySqlTranslator.TryTranslateAnyAll(visitor, node))
            return;

        if ((node.Method.Name == nameof(CommonFunctions.exists)
            || node.Method.Name == nameof(CommonFunctions.all)
            || node.Method.Name == nameof(CommonFunctions.any)
            )
            && node.Arguments is [Expression exp] && exp.Type.IsAssignableTo(typeof(QueryCommand)))
        {
            QueryCommand innerQuery;
            var expVisitor = new TypeExpressionVisitor<ParameterExpression, ConstantExpression>();
            expVisitor.Visit(exp);

            var predicateKeyword = node.Method.Name switch
            {
                nameof(CommonFunctions.exists) => "exists",
                nameof(CommonFunctions.all) => "all",
                nameof(CommonFunctions.any) => "any",
                _ => throw new NotImplementedException()
            };

            var keyCmd = new ExpressionKey(exp, visitor.QueryProvider);
            if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
            {
                object? paramValue;
                Expression body;
                ParameterExpression pExp;
                if (expVisitor.Has1)
                {
                    pExp = Expression.Parameter(typeof(object));
                    var replParam = new ReplaceParameterExpressionVisitor(Expression.Convert(pExp, typeof(IQueryRegistry)));
                    body = replParam.Visit(exp);
                    paramValue = visitor.QueryProvider;
                }
                else if (expVisitor.Has2)
                {
                    pExp = Expression.Parameter(typeof(object));
                    var ce = expVisitor.Target2;
                    var replace = new ReplaceConstantExpressionVisitor(Expression.Convert(pExp, ce!.Type));
                    paramValue = ce.Value;
                    body = replace.Visit(exp);
                }
                else
                    throw new InvalidOperationException();

                var d = Expression.Lambda<Func<object?, object>>(body, pExp).Compile();
                DataContextCache.ExpressionsCache[keyCmd] = d;
                innerQuery = (QueryCommand)d(paramValue);

                if (visitor.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    visitor.Logger.LogTrace("Expression cache miss on visit exists. hashcode: {hash}, value: {value}", keyCmd.GetHashCode(), d(paramValue));
                }
                else if (visitor.Logger?.IsEnabled(LogLevel.Debug) ?? false) visitor.Logger.LogDebug("Expression cache miss on visit exists");
            }
            else
            {
                object? paramValue;

                if (expVisitor.Has1)
                {
                    paramValue = visitor.QueryProvider;
                }
                else if (expVisitor.Has2)
                {
                    var ce = expVisitor.Target2;
                    paramValue = ce!.Value;
                }
                else
                    throw new InvalidOperationException();

                innerQuery = (QueryCommand)((Func<object?, object>)dCmd)(paramValue);
            }

            var sqlBuilder = new SqlBuilder(visitor.Options);
            var sql = sqlBuilder.MakeSelect(innerQuery);

            if (!visitor.IsParamMode)
            {
                visitor.Builder!.Append(visitor.Dialect.MakeSubqueryPredicate(predicateKeyword, sql!, visitor.IsPredicateContext));
            }

            return;
        }
        else if ((node.Method.Name == nameof(CommonFunctions.@in)
            || node.Method.Name == nameof(ClickHouseFunctions.global_in))
            && node.Arguments is [Expression parExp, Expression cmdExp] && cmdExp.Type.IsAssignableTo(typeof(QueryCommand)))
        {
            var globalIn = node.Method.Name == nameof(ClickHouseFunctions.global_in);
            if (globalIn && !visitor.Dialect.SupportsGlobalPredicates)
                throw new NotSupportedException("The GLOBAL IN predicate is not supported by this provider.");

            if (!visitor.IsParamMode)
                visitor.Visit(parExp);

            if (!visitor.IsParamMode) visitor.Builder!.Append(globalIn ? " global in (" : " in (");

            var constRepl = new ReplaceConstantsExpressionVisitor(visitor.QueryProvider);
            var body = constRepl.Visit(cmdExp);

            QueryCommand innerQuery;

            if (constRepl.Params.Count > 0)
            {
                var keyCmd = new ExpressionKey(cmdExp, visitor.QueryProvider);
                if (!DataContextCache.ExpressionsCache.TryGetValue(keyCmd, out var dCmd))
                {
                    var d = Expression.Lambda(body, constRepl.Params.Select(it => it.Item1)).Compile();

                    DataContextCache.ExpressionsCache[keyCmd] = d;
                    innerQuery = (QueryCommand)d.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

                    if (visitor.Logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        visitor.Logger.LogTrace("Subquery expression miss: {exp}", cmdExp);
                    }
                    else if (visitor.Logger?.IsEnabled(LogLevel.Debug) ?? false) visitor.Logger.LogDebug("Subquery expression miss");
                }
                else
                    innerQuery = (QueryCommand)dCmd.DynamicInvoke(constRepl.Params.Select(it => it.Item2).ToArray())!;

            }
            else
                throw new InvalidOperationException();

            var sqlBuilder = new SqlBuilder(visitor.Options);
            var sql = sqlBuilder.MakeSelect(innerQuery);

            if (!visitor.IsParamMode)
            {
                visitor.Builder!.Append(sql).Append(')');
            }

            return;
        }
        else if ((node.Method.Name == nameof(CommonFunctions.@in)
            || node.Method.Name == nameof(ClickHouseFunctions.global_in))
            && node.Arguments is [Expression inColumnExp, Expression inValuesExp]
            && !inValuesExp.Type.IsAssignableTo(typeof(QueryCommand)))
        {
            var globalIn = node.Method.Name == nameof(ClickHouseFunctions.global_in);
            if (globalIn && !visitor.Dialect.SupportsGlobalPredicates)
                throw new NotSupportedException("The GLOBAL IN predicate is not supported by this provider.");

            InValuesTranslator.TranslateInValues(visitor, inColumnExp, inValuesExp, node.Method.GetGenericArguments()[0], globalIn);
            return;
        }
        else if (node.Method.Name == nameof(CommonFunctions.count)
            || node.Method.Name == nameof(CommonFunctions.count_distinct)
            || node.Method.Name == nameof(CommonFunctions.count_big)
            || node.Method.Name == nameof(CommonFunctions.count_big_distinct))
        {
            // count(filter) is the filtered count(*); the plain count takes a params array instead.
            var countFilter = node.Arguments is [Expression filterCandidate] && AggregateFilter.IsFilterExpression(filterCandidate)
                ? filterCandidate
                : null;
            RequireFilter(visitor, countFilter);

            // A count projected into a select list is referenced from outer queries by its property
            // name, so it must carry an alias (the scalar aggregate translators set this too).
            if (!visitor.IsParamMode) visitor.NeedAliasForColumn = true;

            var countStart = visitor.IsParamMode ? 0 : visitor.Builder!.Length;
            var countBig = node.Method.Name.Contains("big", StringComparison.Ordinal);

            if (!visitor.IsParamMode) visitor.Builder!.Append(visitor.Dialect.MakeCount(node.Method.Name.EndsWith("distinct", StringComparison.Ordinal), countBig));

            if (countFilter is not null)
            {
                if (!visitor.IsParamMode) visitor.Builder!.Append('*');
            }
            else if (node.Arguments is [NewArrayExpression newArray])//ReadOnlyCollection<Expression> args
            {
                var items = newArray.Expressions;
                // count() is only valid in SQLite; every other provider (and the SQL standard)
                // requires count(*).
                if (items.Count == 0)
                {
                    if (!visitor.IsParamMode) visitor.Builder!.Append('*');
                }
                else
                {
                    for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                    {
                        var argExp = items[i];
                        using var subVisitor = new BaseExpressionVisitor(visitor.Options with { Dim = 0 });
                        subVisitor.Visit(argExp);
                        if (!visitor.IsParamMode) visitor.Builder!.Append(subVisitor.ToString()).Append(", ");
                    }
                    if (!visitor.IsParamMode) visitor.Builder!.Length -= 2;
                }
            }

            if (!visitor.IsParamMode) visitor.Builder!.Append(')');

            if (countFilter is not null) AggregateFilter.Append(visitor, countFilter);

            if (!visitor.IsParamMode && visitor.Dialect.WrapsCountResult)
            {
                var builder = visitor.Builder!;
                var rendered = builder.ToString(countStart, builder.Length - countStart);
                builder.Length = countStart;
                builder.Append(visitor.Dialect.WrapCount(rendered, countBig));
            }

            return;
        }
        else if (node.Method.Name == nameof(CommonFunctions.min)
            || node.Method.Name == nameof(CommonFunctions.max))
        {
            var args = node.Arguments;
            var minMaxFilter = GetTrailingFilter(args, 2);
            RequireFilter(visitor, minMaxFilter);

            if (!visitor.IsParamMode) visitor.NeedAliasForColumn = true;
            if (!visitor.IsParamMode) visitor.Builder!.Append(node.Method.Name).Append('(');

            var last = minMaxFilter is null ? args.Count : args.Count - 1;
            for (var (i, cnt) = (0, last); i < cnt; i++)
            {
                var argExp = args[i];
                using var subVisitor = new BaseExpressionVisitor(visitor.Options with { Dim = 0 });
                subVisitor.Visit(argExp);
                if (!visitor.IsParamMode) visitor.Builder!.Append(subVisitor.ToString()).Append(", ");
            }

            if (!visitor.IsParamMode)
            {
                if (last > 0) visitor.Builder!.Length -= 2;
                visitor.Builder!.Append(')');
            }

            if (minMaxFilter is not null) AggregateFilter.Append(visitor, minMaxFilter);

            return;
        }
        else if (node.Method.Name == nameof(CommonFunctions.avg)
            || node.Method.Name == nameof(CommonFunctions.sum)
            || node.Method.Name == nameof(CommonFunctions.stdev)
            || node.Method.Name == nameof(CommonFunctions.stdevp)
            || node.Method.Name == nameof(CommonFunctions.var)
            || node.Method.Name == nameof(CommonFunctions.varp)
            || node.Method.Name == nameof(CommonFunctions.avg_distinct)
            || node.Method.Name == nameof(CommonFunctions.sum_distinct)
            || node.Method.Name == nameof(CommonFunctions.stdev_distinct)
            || node.Method.Name == nameof(CommonFunctions.stdevp_distinct)
            || node.Method.Name == nameof(CommonFunctions.var_distinct)
            || node.Method.Name == nameof(CommonFunctions.varp_distinct)
            )
        {
            var args = node.Arguments;
            var aggregateFilter = GetTrailingFilter(args, 2);
            RequireFilter(visitor, aggregateFilter);

            if (!visitor.IsParamMode)
            {
                visitor.NeedAliasForColumn = true;
                visitor.Builder!.Append(visitor.Dialect.MakeAggregate(node.Method.Name.Replace("_distinct", string.Empty))).Append('(');
                if (node.Method.Name.EndsWith("distinct", StringComparison.Ordinal))
                    visitor.Builder!.Append("distinct ");
            }

            var last = aggregateFilter is null ? args.Count : args.Count - 1;
            for (var (i, cnt) = (0, last); i < cnt; i++)
            {
                var argExp = args[i];
                using var subVisitor = new BaseExpressionVisitor(visitor.Options with { Dim = 0 });
                subVisitor.Visit(argExp);
                if (!visitor.IsParamMode) visitor.Builder!.Append(subVisitor.ToString()).Append(", ");
            }

            if (!visitor.IsParamMode)
            {
                if (last > 0) visitor.Builder!.Length -= 2;
                visitor.Builder!.Append(')');
            }

            if (aggregateFilter is not null) AggregateFilter.Append(visitor, aggregateFilter);

            return;
        }

        if (BuiltinFunctionTranslator.TryTranslate(visitor, node))
            return;

        if (SessionInfoFunctionTranslator.TryTranslate(visitor, node))
            return;

        if (UuidFunctionTranslator.TryTranslate(visitor, node))
            return;

        if (AdvancedAggregateTranslator.TryTranslate(visitor, node))
            return;

        if (DateConversionSqlTranslator.TryTranslate(visitor, node))
            return;

        if (ExtendedScalarFunctionTranslator.TryTranslate(visitor, node))
            return;

        if (ArraySqlTranslator.TryTranslateFunction(visitor, node))
            return;

        if (JsonSqlTranslator.TryTranslate(visitor, node))
            return;

        if (TextJsonSqlTranslator.TryTranslate(visitor, node))
            return;

        if (JsonExtractSqlTranslator.TryTranslate(visitor, node))
            return;

        if (DictionarySqlTranslator.TryTranslate(visitor, node))
            return;

        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the trailing filter argument of an aggregate call (<c>agg(x, filter)</c>) when the
    /// argument count matches and the last argument is a filter lambda; otherwise <c>null</c>.
    /// </summary>
    private static Expression? GetTrailingFilter(IReadOnlyList<Expression> args, int withFilterCount)
        => args.Count == withFilterCount && AggregateFilter.IsFilterExpression(args[args.Count - 1])
            ? args[args.Count - 1]
            : null;

    private static void RequireFilter(BaseExpressionVisitor visitor, Expression? filter)
    {
        if (filter is not null && !visitor.Dialect.SupportsFilter)
            throw new NotSupportedException("The FILTER clause is not supported by this provider.");
    }
}
