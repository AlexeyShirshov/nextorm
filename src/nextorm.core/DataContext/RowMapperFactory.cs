using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static readonly MethodInfo GetStreamMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetStream))!;
    private static readonly MethodInfo GetTextReaderMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetTextReader))!;
    private static readonly MethodInfo ToInt64MI = typeof(Convert).GetMethod(nameof(Convert.ToInt64), [typeof(object)])!;
    private static readonly MethodInfo Int64ToInt32MI = typeof(Convert).GetMethod(nameof(Convert.ToInt32), [typeof(long)])!;
    private static readonly MethodInfo GetInt64MI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))!;
    private static readonly MethodInfo FromStorageMI = typeof(DurationStorage).GetMethod(nameof(DurationStorage.FromStorage), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ConvertFromProviderMI = typeof(IPropertyValueConverter).GetMethod(nameof(IPropertyValueConverter.ConvertFromProvider))!;
    private static readonly MethodInfo DynamicColumnsReadMI = typeof(DynamicColumns).GetMethod(nameof(DynamicColumns.Read))!;
    private static readonly MethodInfo GetFieldValueStringMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(typeof(string));
    private static readonly MethodInfo JsonNodeParseMI = typeof(JsonNode).GetMethod(nameof(JsonNode.Parse), [typeof(string), typeof(JsonNodeOptions?), typeof(JsonDocumentOptions)])!;
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
        if (column.IsLobStreaming)
            throw new NotSupportedException(
                $"Column '{column.PropertyName}' is a streaming LOB column and cannot be materialized by the buffered row mapper; stream the row with ToAsyncEnumerable, or read a single LOB column with ToStream/ToTextReader instead.");

        var realType = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;

        var converter = ResolveConverter(column.Converter, dialect);
        if (converter is not null)
            return MapConvertedColumn(column, param, converter);

        Expression getter;
        if (column.IsWideCountNarrowed && realType == typeof(int))
        {
            // #148-B r3 A3′: a navigation Count()/property Count materialized as an int reads the wide
            // (bigint) scalar with GetInt64 and narrows it with a checked conversion. This is uniform
            // across providers and independent of the driver's GetInt32 widening policy (SQLite's
            // GetInt32 silently converts in range and throws out of range; some drivers may not), so an
            // out-of-range count always surfaces a top-level OverflowException.
            var raw = Expression.Call(param, GetInt64MI, Expression.Constant(column.Index));
            getter = Expression.Call(Int64ToInt32MI, raw);
        }
        else if (realType == typeof(TimeSpan) && !supportsNativeDuration)
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
        if (readType == typeof(JsonNode))
        {
            // A bare System.Text.Json.Nodes.JsonNode is not a driver-readable type (Npgsql exposes
            // json/jsonb as JsonDocument/JsonElement): read the JSON text with the generic typed
            // getter and parse it with default options. Only the exact JsonNode type is special-cased;
            // declared JsonObject/JsonArray properties are not supported. Both MethodInfos are resolved
            // once into static fields, so a cached mapper performs no reflection per row.
            var text = Expression.Call(
                Expression.Convert(param, typeof(DbDataReader)),
                GetFieldValueStringMI,
                Expression.Constant(column.Index));
            return Expression.Call(
                JsonNodeParseMI,
                text,
                Expression.Constant(null, typeof(JsonNodeOptions?)),
                Expression.Constant(default(JsonDocumentOptions), typeof(JsonDocumentOptions)));
        }

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
    /// <param name="queryCommand">The prepared command whose projection is mapped.</param>
    /// <param name="sql">The rendered statement text, used as part of the cache key.</param>
    /// <param name="providerType">The concrete context type; its column-mapping policy is part of the cache key.</param>
    /// <param name="logger">The logger used to trace the compiled expression, or <see langword="null"/>.</param>
    /// <param name="mapColumn">The provider's column accessor factory, used for every non-streaming column.</param>
    /// <param name="streaming">
    /// When <see langword="true"/>, a column marked as a streaming LOB projection is read through the
    /// sequential-access <c>GetStream</c>/<c>GetTextReader</c> accessors instead of the buffered path.
    /// The flag is part of the cache key so a streaming mapper and a buffered mapper of the same shape
    /// never share an entry.
    /// </param>
    /// <returns>The compiled row mapper.</returns>
    public static Func<IDataRecord, TResult> GetOrBuild<TResult>(
        QueryCommand<TResult> queryCommand,
        string? sql,
        Type providerType,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn,
        bool streaming = false)
    {
        var key = BuildKey(queryCommand, sql, providerType, streaming);
        if (MapperCache.TryGet(key, out var cached))
            return (Func<IDataRecord, TResult>)cached;

        var map = Build(queryCommand, logger, mapColumn, streaming);
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
        var key = new MapperCacheKey(providerType, typeof(TResult), sql, BuildSignature(selectList), oneColumn, Streaming: false);
        if (MapperCache.TryGet(key, out var cached))
            return (Func<IDataRecord, TResult>)cached;

        var map = Build<TResult>(selectList, oneColumn, null, mapColumn, streaming: false);
        MapperCache.Add(key, map);
        return map;
    }

    /// <summary>
    /// Returns the compiled mapper for a raw command's result set, building and caching it on a miss.
    /// Unlike the SQL-keyed overload this uses a separate key that does not contain the SQL text (raw
    /// commands accept arbitrary text), so the cache cannot grow with the number of distinct statements;
    /// the shape is the reader's ordered column names plus the result type. The select list is produced
    /// lazily by <paramref name="buildSelectList"/> and is only evaluated on a cache miss.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="providerType">The concrete context type; its column-mapping policy is part of the key.</param>
    /// <param name="resultType">The materialized result type (part of the key).</param>
    /// <param name="oneColumn">Whether the result is a single scalar column.</param>
    /// <param name="columns">The reader's ordered column names, or an empty string for a scalar.</param>
    /// <param name="namingConventionType">The naming convention type, when one applies to name matching.</param>
    /// <param name="buildSelectList">Builds the projection; invoked only on a cache miss.</param>
    /// <param name="mapColumn">The provider's column accessor factory.</param>
    /// <param name="recordKind">
    /// The raw-row/composite record shape, or <see cref="RawRowKind.None"/> for an ordinary mapping. Part
    /// of the cache key so a record mapper can never alias an ordinary mapper for the same column key.
    /// </param>
    /// <param name="recordSignature">
    /// The structural record signature (tuple arity/item types), or <see langword="null"/> when the
    /// result type already captures the shape. Part of the cache key.
    /// </param>
    /// <returns>The compiled row mapper.</returns>
    public static Func<IDataRecord, TResult> GetOrBuildRaw<TResult>(
        Type providerType,
        Type resultType,
        bool oneColumn,
        string columns,
        Type? namingConventionType,
        Func<SelectExpression[]> buildSelectList,
        Func<SelectExpression, Expression, Expression> mapColumn,
        RawRowKind recordKind = RawRowKind.None,
        string? recordSignature = null)
    {
        var key = new RawMapperCacheKey(providerType, resultType, oneColumn, columns, namingConventionType, recordKind, recordSignature);
        if (MapperCache.TryGetRaw(key, out var cached))
            return (Func<IDataRecord, TResult>)cached;

        var map = Build<TResult>(buildSelectList(), oneColumn, null, mapColumn, streaming: false);
        MapperCache.AddRaw(key, map);
        return map;
    }

    private static Func<IDataRecord, TResult> Build<TResult>(
        QueryCommand<TResult> queryCommand,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn,
        bool streaming)
    {
#if DEBUG
        if (!queryCommand.IsPrepared)
            throw new InvalidOperationException("Command not prepared");
#endif
        return Build<TResult>(queryCommand.SelectList!, queryCommand.OneColumn, logger, mapColumn, streaming);
    }

    private static Func<IDataRecord, TResult> Build<TResult>(
        SelectExpression[] selectList,
        bool oneColumn,
        ILogger? logger,
        Func<SelectExpression, Expression, Expression> mapColumn,
        bool streaming)
    {
        var resultType = typeof(TResult);
        var param = Expression.Parameter(typeof(IDataRecord));
        Expression<Func<IDataRecord, TResult>> lambda;

        // A streaming LOB column is read through the sequential-access accessor; the buffered path
        // (streaming: false) routes it back through MapColumn, which rejects it with a clear error.
        Expression Accessor(SelectExpression column)
            => column.IsLobStreaming
                ? (streaming ? MapStreamingColumn(column, param) : MapColumn(column, param))
                : mapColumn(column, param);

        if (oneColumn)
        {
            // The provider already evaluated the projection in SQL, so the single column is read
            // directly. Re-evaluating the select expression against the reader would apply a
            // computed expression twice (e.g. a CASE whose test reads a different column).
            var body = Accessor(selectList[0]);

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
                column => Accessor(column),
                (column, startIndex, dynamicColumns) => Expression.Call(
                    Expression.Constant(dynamicColumns),
                    DynamicColumnsReadMI,
                    param,
                    Expression.Constant(startIndex)),
                column => Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index)));

            lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
        }

        if (logger?.IsEnabled(LogLevel.Debug) ?? false) logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);

        return lambda.Compile();
    }

    /// <summary>
    /// Builds the reader accessor for a streaming LOB column: a live <see cref="Stream"/> or
    /// <see cref="TextReader"/> obtained from the sequential-access reader. The value is valid only
    /// until the reader advances to the next row.
    /// </summary>
    /// <param name="column">The streaming column (its <see cref="SelectExpression.PropertyType"/> selects the accessor).</param>
    /// <param name="param">The data-reader expression the accessor is built from.</param>
    /// <returns>An expression that reads the column as a live stream/reader.</returns>
    internal static Expression MapStreamingColumn(SelectExpression column, Expression param)
    {
        var method = column.PropertyType == typeof(TextReader) ? GetTextReaderMI : GetStreamMI;
        Expression call = Expression.Call(Expression.Convert(param, typeof(DbDataReader)), method, Expression.Constant(column.Index));

        if (column.Nullable)
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index)),
                Expression.Constant(null, column.PropertyType),
                call);

        return call;
    }

    /// <summary>
    /// Builds a cheap cache key from the generated SQL plus a column signature. SQL is already
    /// available at the call site, so this is far cheaper than hashing the expression tree.
    /// </summary>
    private static MapperCacheKey BuildKey<TResult>(QueryCommand<TResult> queryCommand, string? sql, Type providerType, bool streaming)
    {
        var signature = BuildSignature(queryCommand.SelectList);
        unchecked
        {
            signature = signature * 31 + (queryCommand.EntityType?.GetHashCode() ?? 0);
        }

        return new MapperCacheKey(providerType, typeof(TResult), sql ?? string.Empty, signature, queryCommand.OneColumn, streaming);
    }

    /// <summary>
    /// True when any projected column was produced by a streaming named-column accessor. Used by the
    /// planner to route a row enumeration that projects a live <see cref="Stream"/>/<see cref="TextReader"/>
    /// through the sequential-access path.
    /// </summary>
    internal static bool HasStreamingColumns(SelectExpression[]? selectList)
    {
        if (selectList is null)
            return false;

        for (var i = 0; i < selectList.Length; i++)
            if (selectList[i].IsLobStreaming)
                return true;

        return false;
    }

    /// <summary>
    /// Validates a row projection that selects live LOB accessors before its SQL is generated. A
    /// sequential-access reader reads columns strictly in ascending ordinal order and a
    /// <see cref="Stream"/>/<see cref="TextReader"/> obtained from it is only valid until the next read,
    /// so a row projection can contain at most one such column and it must be the last projected column.
    /// Rejecting the shape here turns a driver-level failure or a silently broken mapper into an
    /// actionable error.
    /// </summary>
    /// <param name="selectList">The projected columns, in result-set order.</param>
    /// <exception cref="NotSupportedException">More than one streaming column, or a streaming column that is not the last projected one.</exception>
    internal static void ValidateStreamingColumns(SelectExpression[]? selectList)
    {
        if (selectList is null)
            return;

        var streamingIndex = -1;
        for (var i = 0; i < selectList.Length; i++)
        {
            if (!selectList[i].IsLobStreaming)
                continue;

            if (streamingIndex >= 0)
                throw new NotSupportedException(
                    "A row projection can contain at most one streaming LOB column (a Stream/TextReader selected with GetStream/GetTextReader/AsStream/AsTextReader); project the other large values as buffered byte[]/string columns.");

            streamingIndex = i;
        }

        if (streamingIndex >= 0 && streamingIndex != selectList.Length - 1)
            throw new NotSupportedException(
                $"The streaming LOB column '{selectList[streamingIndex].PropertyName}' must be the last projected column, because sequential access reads columns in ordinal order; move it to the end of the projection.");
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
                    signature = signature * 31 + (column.IsWideCountNarrowed ? 1 : 0);
                    signature = signature * 31 + (column.PropertyName?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.DurationUnit?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.ProviderType?.GetHashCode() ?? 0);
                    signature = signature * 31 + (column.Converter is null ? 0 : RuntimeHelpers.GetHashCode(column.Converter));
                    signature = signature * 31 + (column.ProjectionItem is { } item ? item.EntityType.GetHashCode() * 31 + item.Slot : 0);
                }
            }
        }

        return signature;
    }
}
