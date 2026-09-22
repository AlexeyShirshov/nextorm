using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Produces the compiled row mapper for a prepared command: builds the expression tree (via
/// <see cref="RowMaterializerBuilder"/>), compiles it and caches it by SQL text. Extracted from
/// <see cref="DataContext"/> (see docs/specs/design/solid-review.md, F1) because the work is provider-agnostic — the only
/// provider-specific part is how a column is read, and that is supplied as the <c>mapColumn</c>
/// delegate instead of an abstraction (there is no second consumer that would need one).
/// </summary>
internal static class RowMapperFactory
{
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;

    /// <summary>
    /// Default column accessor: typed getters only (no <c>GetValue</c>/boxing), with the ordinal baked
    /// in as a constant. Providers whose reader does not widen CLR types (SqlClient throws when a typed
    /// getter does not match the field type) substitute their own.
    /// </summary>
    public static Expression MapColumn(SelectExpression column, Expression param)
    {
        var method = column.GetDataRecordMethod();
        var accessor = method.DeclaringType == typeof(IDataRecord)
            ? param
            : Expression.Convert(param, method.DeclaringType!);
        var getter = Expression.Call(accessor, method, Expression.Constant(column.Index));

        if (column.Nullable)
        {
            // The getter returns the underlying type (long for long?, string for string).
            // Only nullable value types need a Convert; reference types are already exact.
            Expression value = getter.Type == column.PropertyType
                ? getter
                : Expression.Convert(getter, column.PropertyType);

            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index)),
                Expression.Constant(null, column.PropertyType),
                value);
        }

        if (column.DefaultOnNull)
        {
            // A non-nullable value projection from a *OrDefault scalar terminal: SQL NULL means the
            // subquery matched no row, so substitute default(T) instead of letting the getter throw.
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index)),
                Expression.Default(column.PropertyType),
                getter);
        }

        return getter;
    }

    /// <summary>
    /// Returns the compiled mapper for the command, building and caching it on a miss. The SQL text is
    /// already produced at the call site, so the cache key stays cheap (see <see cref="MapperCacheKey"/>).
    /// </summary>
    public static Func<IDataRecord, TResult> GetOrBuild<TResult>(
        QueryCommand<TResult> queryCommand,
        string? sql,
        Type providerType,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn)
    {
        var key = BuildKey(queryCommand, sql, providerType);
        if (MapperCache.TryGet(key, out var cached))
            return (Func<IDataRecord, TResult>)cached;

        var map = Build(queryCommand, logger, mapColumn);
        MapperCache.Add(key, map);
        return map;
    }

    private static Func<IDataRecord, TResult> Build<TResult>(
        QueryCommand<TResult> queryCommand,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn)
    {
#if DEBUG
        if (!queryCommand.IsPrepared)
            throw new InvalidOperationException("Command not prepared");
#endif
        var resultType = typeof(TResult);
        var param = Expression.Parameter(typeof(IDataRecord));
        Expression<Func<IDataRecord, TResult>> lambda;

        if (queryCommand.OneColumn)
        {
            // The provider already evaluated the projection in SQL, so the single column is read
            // directly. Re-evaluating the select expression against the reader would apply a
            // computed expression twice (e.g. a CASE whose test reads a different column).
            var body = mapColumn(queryCommand.SelectList![0], param);
            lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
        }
        else
        {
            var body = RowMaterializerBuilder.Build(
                resultType,
                param,
                queryCommand.SelectList!,
                ignoreColumns: false,
                column => mapColumn(column, param));

            lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
        }

        if (logger?.IsEnabled(LogLevel.Debug) ?? false) logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);

        return lambda.Compile();
    }

    /// <summary>
    /// Builds a cheap cache key from the generated SQL plus a column signature. SQL is already
    /// available at the call site, so this is far cheaper than hashing the expression tree.
    /// </summary>
    private static MapperCacheKey BuildKey<TResult>(QueryCommand<TResult> queryCommand, string? sql, Type providerType)
    {
        var signature = 7;
        var selectList = queryCommand.SelectList;
        if (selectList is not null)
        {
            unchecked
            {
                signature = signature * 31 + (queryCommand.EntityType?.GetHashCode() ?? 0);
                for (var i = 0; i < selectList.Length; i++)
                {
                    var column = selectList[i];
                    signature = signature * 31 + column.Index;
                    signature = signature * 31 + (column.PropertyType?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.Nullable ? 1 : 0);
                    signature = signature * 31 + (column.DefaultOnNull ? 1 : 0);
                    signature = signature * 31 + (column.PropertyName?.GetHashCode() ?? 0);
                }
            }
        }

        return new MapperCacheKey(providerType, typeof(TResult), sql ?? string.Empty, signature, queryCommand.OneColumn);
    }
}
