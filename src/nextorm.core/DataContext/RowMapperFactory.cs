using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private static readonly MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo ToInt64MI = typeof(Convert).GetMethod(nameof(Convert.ToInt64), [typeof(object)])!;
    private static readonly MethodInfo FromStorageMI = typeof(DurationStorage).GetMethod(nameof(DurationStorage.FromStorage), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ConvertFromProviderMI = typeof(IPropertyValueConverter).GetMethod(nameof(IPropertyValueConverter.ConvertFromProvider))!;
    private static readonly ConcurrentDictionary<Type, MethodInfo?> TypedFromProviderMethods = new();

    /// <summary>
    /// Default column accessor: typed getters only (no <c>GetValue</c>/boxing), with the ordinal baked
    /// in as a constant. Providers whose reader does not widen CLR types (SqlClient throws when a typed
    /// getter does not match the field type) substitute their own.
    /// </summary>
    /// <param name="column">The projected column being read.</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <param name="supportsNativeDuration">
    /// When <see langword="false"/>, a <see cref="TimeSpan"/> column is read as its stored integer and
    /// converted with the column's <see cref="DurationUnit"/> (default <see cref="DurationUnit.Ticks"/>);
    /// when <see langword="true"/> the driver exposes the native duration type directly.
    /// </param>
    /// <param name="dialect">
    /// The active dialect, used to resolve a dialect-dependent converter (a JSON column whose storage is
    /// <see cref="JsonColumnStorage.Auto"/>) before reading. Ignored when the column has no converter.
    /// </param>
    public static Expression MapColumn(SelectExpression column, Expression param, bool supportsNativeDuration = true, ISqlDialect? dialect = null)
    {
        var realType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        var converter = ResolveConverter(column.Converter, dialect);
        if (converter is not null)
            return MapConvertedColumn(column, param, converter);

        Expression getter;
        if (realType == typeof(TimeSpan) && !supportsNativeDuration)
        {
            // The provider keeps the duration in an integer column: read the boxed value, widen it to
            // long and reinterpret it in the declared unit.
            var unit = column.DurationUnit ?? DurationUnit.Ticks;
            var raw = Expression.Call(param, GetValueMI, Expression.Constant(column.Index));
            getter = Expression.Call(FromStorageMI, Expression.Call(ToInt64MI, raw), Expression.Constant(unit));
        }
        else
        {
            getter = GetReaderAccessor(column, param, realType);
        }

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

    private static IPropertyValueConverter? ResolveConverter(IPropertyValueConverter? converter, ISqlDialect? dialect)
        => converter is IJsonColumnConverter json && dialect is not null ? json.Resolve(dialect) : converter;

    private static Expression GetReaderAccessor(SelectExpression column, Expression param, Type readType)
    {
        var method = column.GetDataRecordMethod(readType);
        var accessor = method.DeclaringType == typeof(IDataRecord)
            ? param
            : Expression.Convert(param, method.DeclaringType!);
        return Expression.Call(accessor, method, Expression.Constant(column.Index));
    }

    private static Expression MapConvertedColumn(SelectExpression column, Expression param, IPropertyValueConverter converter)
    {
        var providerType = converter.ProviderType;
        var getter = GetReaderAccessor(column, param, providerType);
        var isDbNull = Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index));

        if (converter.ConvertsNulls)
        {
            // The converter owns the null policy: SQL NULL is passed through as the default provider value.
            Expression nullProviderValue = providerType.IsValueType
                ? Expression.Default(providerType)
                : Expression.Constant(null, providerType);
            return ConvertFromProvider(converter, Expression.Condition(isDbNull, nullProviderValue, getter), column.PropertyType);
        }

        var converted = ConvertFromProvider(converter, getter, column.PropertyType);

        if (column.Nullable)
            return Expression.Condition(isDbNull, Expression.Constant(null, column.PropertyType), converted);

        if (column.DefaultOnNull)
            return Expression.Condition(isDbNull, Expression.Default(column.PropertyType), converted);

        return converted;
    }

    private static Expression ConvertFromProvider(IPropertyValueConverter converter, Expression providerValue, Type modelType)
    {
        // Prefer the closed generic typed method so a value-type model is not boxed per row; fall back
        // to the interface bridge for a converter implemented directly against IPropertyValueConverter.
        var typedMethod = TypedFromProviderMethods.GetOrAdd(converter.GetType(), static type => ValueConverterReflection.GetFromProviderMethod(type));
        Expression call;
        if (typedMethod is not null && typedMethod.GetParameters()[0].ParameterType == providerValue.Type)
            call = Expression.Call(Expression.Constant(converter, typedMethod.DeclaringType!), typedMethod, providerValue);
        else
            call = Expression.Call(Expression.Constant(converter), ConvertFromProviderMI, Expression.Convert(providerValue, typeof(object)));

        return call.Type == modelType ? call : Expression.Convert(call, modelType);
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

    /// <summary>
    /// Returns the compiled mapper for a mutation that returns rows (an <c>INSERT ... RETURNING</c>/
    /// <c>OUTPUT</c>), building and caching it on a miss. Unlike the query overload there is no
    /// <see cref="QueryCommand{TResult}"/>; the projection is carried by the caller as an explicit
    /// select list, so the same materialization path is reused.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="sql">The rendered statement text, used as part of the cache key.</param>
    /// <param name="providerType">The concrete context type; its column-mapping policy is part of the cache key.</param>
    /// <param name="selectList">The returned columns, in result-set order.</param>
    /// <param name="oneColumn">Whether the projection is a single scalar column.</param>
    /// <param name="mapColumn">The provider's column accessor factory.</param>
    /// <returns>The compiled row mapper.</returns>
    public static Func<IDataRecord, TResult> GetOrBuild<TResult>(
        string sql,
        Type providerType,
        SelectExpression[] selectList,
        bool oneColumn,
        Func<SelectExpression, Expression, Expression> mapColumn)
    {
        var key = new MapperCacheKey(providerType, typeof(TResult), sql, BuildSignature(selectList), oneColumn);
        if (MapperCache.TryGet(key, out var cached))
            return (Func<IDataRecord, TResult>)cached;

        var map = Build<TResult>(selectList, oneColumn, null, mapColumn);
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
        return Build<TResult>(queryCommand.SelectList!, queryCommand.OneColumn, logger, mapColumn);
    }

    private static Func<IDataRecord, TResult> Build<TResult>(
        SelectExpression[] selectList,
        bool oneColumn,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn)
    {
        var resultType = typeof(TResult);
        var param = Expression.Parameter(typeof(IDataRecord));
        Expression<Func<IDataRecord, TResult>> lambda;

        if (oneColumn)
        {
            // The provider already evaluated the projection in SQL, so the single column is read
            // directly. Re-evaluating the select expression against the reader would apply a
            // computed expression twice (e.g. a CASE whose test reads a different column).
            var body = mapColumn(selectList[0], param);

            // The column carries the source CLR type, but the scalar terminal may project it as
            // another type (Returning(x => (long)x.IntCol), ReturningKey<long?>()); widen the read.
            if (body.Type != resultType)
                body = Expression.Convert(body, resultType);

            lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
        }
        else
        {
            var body = RowMaterializerBuilder.Build(
                resultType,
                param,
                selectList,
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
        var signature = BuildSignature(queryCommand.SelectList);
        unchecked
        {
            signature = signature * 31 + (queryCommand.EntityType?.GetHashCode() ?? 0);
        }

        return new MapperCacheKey(providerType, typeof(TResult), sql ?? string.Empty, signature, queryCommand.OneColumn);
    }

    private static int BuildSignature(SelectExpression[]? selectList)
    {
        var signature = 7;
        if (selectList is not null)
        {
            unchecked
            {
                for (var i = 0; i < selectList.Length; i++)
                {
                    var column = selectList[i];
                    signature = signature * 31 + column.Index;
                    signature = signature * 31 + (column.PropertyType?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.Nullable ? 1 : 0);
                    signature = signature * 31 + (column.DefaultOnNull ? 1 : 0);
                    signature = signature * 31 + (column.PropertyName?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.DurationUnit?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.ProviderType?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.Converter is null ? 0 : RuntimeHelpers.GetHashCode(column.Converter));
                }
            }
        }

        return signature;
    }
}
