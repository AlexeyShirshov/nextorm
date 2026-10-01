using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Builds (and caches) the row mapper for a raw command's result set. Two shapes are supported,
/// matching the scope of <c>ExecuteRaw</c>:
/// <list type="bullet">
/// <item><description>a scalar result type (primitive, string, decimal,
/// <see cref="DateTime"/>/<see cref="DateTimeOffset"/>/<see cref="TimeSpan"/>,
/// <see cref="DateOnly"/>/<see cref="TimeOnly"/>, <see cref="Guid"/>, <c>byte[]</c>, enum or a nullable
/// of these) reads column 0 of the current result set;</description></item>
/// <item><description>a mapped entity result type is materialized by <b>column name</b>: the entity's
/// projection is reordered/filtered to the reader's columns (case-insensitive), properties absent from
/// the reader are skipped, and unmatched reader columns are ignored. If no column matches, the read fails
/// with a clear <see cref="InvalidOperationException"/>.</description></item>
/// </list>
/// Name matching is done in two passes: first against the mapped column name (naming convention applied
/// for auto names), then against the CLR property name for properties still unmatched. Each reader
/// column binds at most one property, and each property/projection slot binds at most once. A duplicated
/// reader column name binds its first occurrence and is ignored afterwards.
/// </summary>
/// <remarks>
/// An entity result type must be a <b>concrete class with a public parameterless constructor</b>: mapping
/// binds by column name through member-init, and a constructor with parameters would bind the projected
/// columns positionally (<c>EnsureNameMappable</c> rejects it).
/// <para>
/// The per-type raw select-list cache (<c>RawSelectListCache</c>) and the mapper key
/// (<see cref="RowMapperFactory.GetOrBuildRaw{TResult}"/>, keyed by provider type, result type, column
/// shape and <see cref="INamingConvention"/> type) assume the registered entity metadata is
/// <b>immutable</b> and the naming convention is <b>stateless</b> — they are keyed by type, not
/// snapshotted. Do not mutate a mapping or use a stateful convention while raw mappers are cached.
/// </para>
/// </remarks>
internal static class RawMapperFactory
{
    private static readonly MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetValueMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly MethodInfo ConvertMI = typeof(RawValueConverter).GetMethod(nameof(RawValueConverter.Convert))!;

    // Raw projections start from the entity metadata, not from the shared DataContextCache.SelectListCache:
    // that cache is reserved for LINQ/plan-cached projections, whose entries carry a PlanHashCode. Writing
    // a raw entry there would change a later LINQ query's ColumnsPlanHash (the entry would contribute zero),
    // so raw keeps its own Type-keyed copy. Reading the shared cache is intentionally not done either, so
    // the raw and LINQ shapes can never influence each other.
    private static readonly ConcurrentDictionary<Type, SelectExpression[]> RawSelectListCache = new();

    /// <summary>Clears the raw per-type projection cache; called by <see cref="DataContextCache.Clear"/>.</summary>
    internal static void Clear() => RawSelectListCache.Clear();

    /// <summary>
    /// Returns the compiled mapper for the current result set of <paramref name="reader"/>. Mappers are
    /// cached by result shape (reader column names + type), never by SQL text, so arbitrary raw statements
    /// do not grow the cache. See <see cref="RowMapperFactory.GetOrBuildRaw{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The scalar or mapped-entity result type.</typeparam>
    /// <param name="context">The context supplying the provider type, naming convention and column mapping.</param>
    /// <param name="reader">The reader positioned on the result set whose columns drive the shape.</param>
    /// <returns>A compiled mapper from a data record to <typeparamref name="T"/>.</returns>
    internal static Func<IDataRecord, T> GetOrBuild<T>(DataContext context, DbDataReader reader)
    {
        var resultType = typeof(T);
        var providerType = context.GetType();

        if (IsScalarType(resultType))
        {
            return RowMapperFactory.GetOrBuildRaw<T>(
                providerType,
                resultType,
                oneColumn: true,
                columns: string.Empty,
                namingConventionType: null,
                buildSelectList: () => [new SelectExpression(resultType) { Index = 0 }],
                mapColumn: MapScalarColumn);
        }

        var entityMeta = DataContextCache.Metadata.TryGetValue(resultType, out var existing)
            ? existing
            : DataContextExtensions.ResolveMetadata<T>(context, null);

        if (entityMeta.Properties.Count == 0)
            throw Unsupported(resultType);

        return RowMapperFactory.GetOrBuildRaw<T>(
            providerType,
            resultType,
            oneColumn: false,
            columns: BuildColumnsKey(reader),
            namingConventionType: context.NamingConvention?.GetType(),
            buildSelectList: () => BuildEntitySelectList(context, reader, resultType, entityMeta),
            mapColumn: context.MapColumnExpression);
    }

    private static NotSupportedException Unsupported(Type resultType)
        => new(
            $"ExecuteRaw supports mapped entity types and scalar types; {resultType.Name} is neither. "
            + $"Register a mapping with From<{resultType.Name}>() first, or read the result as a scalar.");

    /// <summary>
    /// Raw entity mapping binds by column name, which is expressed as member-init. A constructor with
    /// parameters would make <see cref="RowMaterializerBuilder"/> bind positionally (its longest
    /// constructor is used when its parameter count matches the projection), which can silently
    /// mis-map under a reader-ordered projection; reject it with an actionable message instead.
    /// </summary>
    private static void EnsureNameMappable(Type resultType)
    {
        var ctor = resultType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor is null || ctor.GetParameters().Length != 0)
            throw new NotSupportedException(
                $"Raw command mapping is by column name and requires {resultType.Name} to have a public parameterless constructor; "
                + "a constructor with parameters would bind the projected columns positionally, which is not supported for raw result sets.");
    }

    private static Expression MapScalarColumn(SelectExpression column, Expression record)
    {
        var index = Expression.Constant(column.Index);
        var isDbNull = Expression.Call(record, IsDBNullMI, index);
        var value = Expression.Call(ConvertMI.MakeGenericMethod(column.PropertyType), Expression.Call(record, GetValueMI, index));

        return column.PropertyType.IsValueType
            ? Expression.Condition(isDbNull, Expression.Default(column.PropertyType), value)
            : Expression.Condition(isDbNull, Expression.Constant(null, column.PropertyType), value);
    }

    private static SelectExpression[] BuildEntitySelectList(DataContext context, DbDataReader reader, Type resultType, IEntityMetadata entityMeta)
    {
        EnsureNameMappable(resultType);

        var baseList = GetBaseSelectList(resultType, entityMeta);
        var properties = new Dictionary<PropertyInfo, IPropertyMetadata>();
        foreach (var prop in entityMeta.Properties)
            properties[prop.PropertyInfo] = prop;

        // matched[c] is the clone bound to base projection slot c (null when unmatched); usedReader
        // ensures a reader column binds at most one slot, matched[c] ensures a slot binds at most once.
        var matched = new SelectExpression?[baseList.Length];
        var usedReader = new bool[reader.FieldCount];

        // Pass 1: the mapped column name (naming convention applied). This is what makes a property X
        // mapped to column "y" win over a property named Y also present in the reader.
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (usedReader[i])
                continue;

            var readerName = reader.GetName(i);
            for (var c = 0; c < baseList.Length; c++)
            {
                if (matched[c] is not null || !MatchesMappedColumn(baseList[c], properties, context.NamingConvention, readerName))
                    continue;

                matched[c] = CloneWithIndex(baseList[c], i);
                usedReader[i] = true;
                break;
            }
        }

        // Pass 2: the CLR property name, only for slots still unmatched. Range slots are excluded (they
        // bind through their declared lower/upper column names in pass 1).
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (usedReader[i])
                continue;

            var readerName = reader.GetName(i);
            for (var c = 0; c < baseList.Length; c++)
            {
                if (matched[c] is not null || !MatchesPropertyName(baseList[c], readerName))
                    continue;

                matched[c] = CloneWithIndex(baseList[c], i);
                usedReader[i] = true;
                break;
            }
        }

        // Range<T> materialization (RowMaterializerBuilder) expects a lower slot immediately followed
        // by its upper slot. Emit matched slots in metadata order, pairing them, and drop an incomplete
        // pair (only one of the two columns present in the reader).
        var result = new List<SelectExpression>(baseList.Length);
        for (var c = 0; c < baseList.Length; c++)
        {
            var column = matched[c];
            if (column is null || column.RangeColumnRole == RangeColumnRole.Upper)
                continue;

            if (column.RangeColumnRole == RangeColumnRole.Lower)
            {
                var upper = FindMatchingUpper(matched, baseList, column);
                if (upper is null)
                    continue;

                result.Add(column);
                result.Add(upper);
                continue;
            }

            result.Add(column);
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                $"None of the result-set columns ({DescribeColumns(reader)}) matches a mapped property of {resultType.Name}.");

        return result.ToArray();
    }

    private static SelectExpression? FindMatchingUpper(SelectExpression?[] matched, SelectExpression[] baseList, SelectExpression lower)
    {
        for (var c = 0; c < baseList.Length; c++)
        {
            if (baseList[c].RangeColumnRole == RangeColumnRole.Upper
                && ReferenceEquals(baseList[c].PropertyInfo, lower.PropertyInfo))
                return matched[c];
        }

        return null;
    }

    private static bool MatchesMappedColumn(SelectExpression column, Dictionary<PropertyInfo, IPropertyMetadata> properties, INamingConvention? namingConvention, string readerName)
    {
        if (column.RangeColumnRole == RangeColumnRole.Lower && column.RangeColumns is { } lower)
            return NameEquals(lower.LowerColumn, readerName);
        if (column.RangeColumnRole == RangeColumnRole.Upper && column.RangeColumns is { } upper)
            return NameEquals(upper.UpperColumn, readerName);

        if (column.PropertyInfo is { } propertyInfo && properties.TryGetValue(propertyInfo, out var prop))
            return NameEquals(SqlMutationBuilder.ResolveColumnName(prop, namingConvention), readerName);

        return false;
    }

    private static bool MatchesPropertyName(SelectExpression column, string readerName)
    {
        if (column.RangeColumnRole != RangeColumnRole.None)
            return false;

        return column.PropertyInfo is { } propertyInfo
            ? NameEquals(propertyInfo.Name, readerName)
            : NameEquals(column.PropertyName, readerName);
    }

    private static SelectExpression[] GetBaseSelectList(Type resultType, IEntityMetadata entityMeta)
        => RawSelectListCache.GetOrAdd(resultType, _ => EntitySelectListBuilder.Build(resultType, entityMeta, CancellationToken.None).Columns);

    // A clone is mandatory: the base projection is shared per type, so mutating its Index to point at a
    // reader ordinal would corrupt any other raw read of the same type.
    private static SelectExpression CloneWithIndex(SelectExpression source, int index) => new(source.PropertyType)
    {
        Index = index,
        PropertyName = source.PropertyName,
        Expression = source.Expression,
        PropertyInfo = source.PropertyInfo,
        DurationUnit = source.DurationUnit,
        DurationPrecision = source.DurationPrecision,
        ProviderType = source.ProviderType,
        Converter = source.Converter,
        DefaultOnNull = source.DefaultOnNull,
        RangeColumnRole = source.RangeColumnRole,
        RangeColumns = source.RangeColumns,
    };

    private static string BuildColumnsKey(DbDataReader reader)
    {
        if (reader.FieldCount == 0)
            return string.Empty;

        // The shape key is the exact ordered reader column names; it is cheap to build and lets a
        // cached mapper be reused for the same shape without touching the metadata/property dictionary.
        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return string.Join("\u001f", names);
    }

    private static bool NameEquals(string? a, string? b)
        => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string DescribeColumns(DbDataReader reader)
    {
        if (reader.FieldCount == 0)
            return string.Empty;

        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return string.Join(", ", names);
    }

    internal static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(Guid)
            || type == typeof(byte[]);
    }
}

/// <summary>
/// Converts a boxed provider value to the requested scalar type for <see cref="RawMapperFactory"/>.
/// Kept as a static entry point so the compiled mapper can call it without boxing the converter.
/// </summary>
internal static class RawValueConverter
{
    /// <summary>Converts <paramref name="value"/> to <typeparamref name="T"/>, unwrapping a nullable target when present.</summary>
    /// <typeparam name="T">The requested scalar type.</typeparam>
    /// <param name="value">The boxed provider value (never <see cref="DBNull"/>; the caller handles nulls).</param>
    /// <returns>The converted value.</returns>
    public static T Convert<T>(object value)
    {
        if (value is T typed)
            return typed;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)ConvertTo(value, target)!;
    }

    private static object? ConvertTo(object? value, Type target)
    {
        if (value is null)
            return null;

        if (target.IsInstanceOfType(value))
            return value;

        if (target.IsEnum)
            return value is string text ? Enum.Parse(target, text, ignoreCase: true) : Enum.ToObject(target, value);

        if (target == typeof(Guid))
            return value is Guid guid ? guid : Guid.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!);

        if (target == typeof(DateOnly))
            return value is DateOnly dateOnly ? dateOnly : DateOnly.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        if (target == typeof(TimeOnly))
            return value is TimeOnly timeOnly ? timeOnly : TimeOnly.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        if (target == typeof(DateTimeOffset))
            return value is DateTimeOffset offset ? offset : new DateTimeOffset(System.Convert.ToDateTime(value, CultureInfo.InvariantCulture));

        if (target == typeof(TimeSpan))
            return value is TimeSpan timeSpan ? timeSpan : TimeSpan.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

        return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
