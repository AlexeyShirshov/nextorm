using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// Translation of a <see cref="TableParameterValue"/> into the provider representation: columns
/// (including value-converter and enum/duration normalization), a JSON document and a typed array. Kept
/// internal; providers use the public members on <see cref="TableParameterValue"/>.
/// </summary>
internal static class TableParameterBinder
{
    /// <summary>Whether the row type maps to one column (a scalar) rather than to a mapped entity.</summary>
    internal static bool IsScalarRowType(Type rowType) => RawMapperFactory.IsScalarType(rowType);

    /// <summary>
    /// Resolves the columns bound for a table parameter: one synthetic <c>Value</c> column for a scalar
    /// row type, or the entity's non-computed mapped properties in metadata order (including identity
    /// columns). A <see cref="Range{T}"/> property maps to two columns and is not supported.
    /// </summary>
    /// <param name="value">The table parameter value.</param>
    /// <param name="context">The context supplying the naming convention and dialect.</param>
    /// <returns>The columns, in binding order.</returns>
    /// <exception cref="NotSupportedException">The type has no mapped column, maps a two-column range, or a property maps through a value converter to <see cref="TimeSpan"/> on a provider without a native duration type.</exception>
    /// <exception cref="InvalidOperationException">A column declares a decimal precision/scale whose bound provider type is not decimal, or as a partial pair.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A column declares a decimal precision/scale outside the allowed range (precision 1..38, scale 0..precision).</exception>
    internal static IReadOnlyList<TableParameterColumn> BuildColumns(TableParameterValue value, DataContext context)
    {
        if (IsScalarRowType(value.RowType))
            return [new TableParameterColumn("Value", ReduceForStorage(value.RowType, context.Dialect), row => Normalize(row, null, context.Dialect))];

        var properties = ResolvePropertyColumns(value.RowType);
        var columns = new TableParameterColumn[properties.Count];

        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var clrType = ResolveProviderClrType(property, context.Dialect);

            // Validate the declared pair against the type actually bound for the column. This runs once
            // per column (metadata construction), never per row, and catches an external
            // IPropertyMetadata that bypassed the mapping builders before a provider narrows (SQL Server
            // casts precision/scale to byte) or formats (ClickHouse renders Decimal(p, s)) the value.
            DecimalPrecisionRules.Validate(clrType, property.PropertyInfo.Name, property.DecimalPrecision, property.DecimalScale);

            columns[i] = new TableParameterColumn(
                ResolveColumnName(property, context.NamingConvention),
                clrType,
                row => Normalize(property.PropertyInfo.GetValue(row), property, context.Dialect),
                property.DecimalPrecision,
                property.DecimalScale);
        }

        return columns;
    }

    private static IReadOnlyList<IPropertyMetadata> ResolvePropertyColumns(Type rowType)
    {
        var metadata = DataContextExtensions.ResolveMetadata(rowType);
        var columns = new List<IPropertyMetadata>(metadata.Properties.Count);

        foreach (var property in metadata.Properties)
        {
            if (property.IsComputed)
                continue;

            if (property.RangeColumns is not null)
                throw new NotSupportedException(
                    $"Table-valued parameter row type {rowType.Name} maps property {property.PropertyInfo.Name} to two columns (Range<T>), which is not supported.");

            columns.Add(property);
        }

        if (columns.Count == 0)
            throw new NotSupportedException($"Table-valued parameter row type {rowType.Name} has no mapped column to bind.");

        return columns;
    }

    /// <summary>Resolves the mapped column name (naming convention applied to auto names) of a column.</summary>
    /// <param name="property">The mapped property.</param>
    /// <param name="namingConvention">The active naming convention, or <see langword="null"/>.</param>
    /// <returns>The column name.</returns>
    internal static string ResolveColumnName(IPropertyMetadata property, INamingConvention? namingConvention)
        => SqlMutationBuilder.ResolveColumnName(property, namingConvention);

    /// <summary>Normalizes a value for the provider (converter, duration storage, then enum unwrap).</summary>
    private static object? Normalize(object? value, IPropertyMetadata? property, ISqlDialect dialect)
    {
        var normalized = DurationStorage.ToParameterValue(value, property, dialect);

        if (normalized is not null && normalized.GetType().IsEnum)
            return Convert.ChangeType(normalized, Enum.GetUnderlyingType(normalized.GetType()), CultureInfo.InvariantCulture);

        return normalized;
    }

    /// <summary>
    /// Resolves the CLR type bound for a column: the value converter's provider type when one is mapped
    /// (resolving a dialect-dependent JSON converter first), otherwise the property type; an enum is
    /// reduced to its underlying numeric type and, on a provider without a native duration type, a
    /// <see cref="TimeSpan"/> to its integer storage type.
    /// </summary>
    /// <exception cref="NotSupportedException">A value converter targets <see cref="TimeSpan"/> on a provider without a native duration type.</exception>
    private static Type ResolveProviderClrType(IPropertyMetadata property, ISqlDialect dialect)
    {
        var converterType = ResolveConverter(property, dialect)?.ProviderType;
        RejectConverterToDuration(property, converterType, dialect);
        return ReduceForStorage(converterType ?? property.PropertyInfo.PropertyType, dialect);
    }

    /// <summary>
    /// Rejects a value converter whose provider type is <see cref="TimeSpan"/> on a provider without a
    /// native duration type, at column construction and before any row is consumed. The general write
    /// seam lets such a converter own the provider representation (the converted <see cref="TimeSpan"/> is
    /// what the provider binds), but a table-valued parameter has no native duration binding: the
    /// converted value cannot be written as a stored integer unit here, so the failure is explicit
    /// instead of deferring to an unsupported record value.
    /// </summary>
    private static void RejectConverterToDuration(IPropertyMetadata property, Type? converterType, ISqlDialect dialect)
    {
        if (converterType is null || dialect.SupportsNativeDuration)
            return;

        if ((Nullable.GetUnderlyingType(converterType) ?? converterType) == typeof(TimeSpan))
            throw new NotSupportedException(
                $"Table-valued parameter row type property {property.PropertyInfo.Name} maps through a value converter to TimeSpan, "
                + "but this provider has no native duration type; bind the converted value as its integer storage instead.");
    }

    /// <summary>
    /// Reduces a bound type to its provider storage representation: an enum to its underlying numeric
    /// type and, on a provider without a native duration type, a <see cref="TimeSpan"/> to the integer
    /// type it is stored as. Nullability is preserved; any other type is returned unchanged.
    /// </summary>
    private static Type ReduceForStorage(Type type, ISqlDialect dialect)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        Type? storage = underlying.IsEnum
            ? Enum.GetUnderlyingType(underlying)
            : underlying == typeof(TimeSpan) && !dialect.SupportsNativeDuration
                ? typeof(long)
                : null;

        if (storage is null)
            return type;

        return Nullable.GetUnderlyingType(type) is null ? storage : typeof(Nullable<>).MakeGenericType(storage);
    }

    private static IPropertyValueConverter? ResolveConverter(IPropertyMetadata property, ISqlDialect dialect)
    {
        var converter = property.Converter;

        // A [JsonColumn] mapping stores a dialect-aware wrapper; resolve it so the provider type matches
        // the storage the value is normalized to.
        if (converter is IJsonColumnConverter json)
            converter = json.Resolve(dialect);

        return converter;
    }

    /// <summary>
    /// Serializes the parameter as a JSON array: an array of scalar values for a scalar row type, or an
    /// array of objects keyed by the mapped column name for an entity. Written directly with
    /// <see cref="Utf8JsonWriter"/> (no reflection serializer): nulls become JSON <c>null</c>, dates and
    /// times are ISO-8601 strings, <c>byte[]</c> is base64, enums are their underlying number, and a
    /// duration on a provider without a native duration type is its stored integer. <c>NaN</c>/<c>±∞</c>
    /// have no JSON representation and throw.
    /// </summary>
    /// <exception cref="NotSupportedException">A row holds a value with no JSON representation (<c>NaN</c>/<c>±∞</c>) or an unsupported CLR type.</exception>
    internal static void WriteJson(TableParameterValue value, Utf8JsonWriter writer, DataContext context)
    {
        writer.WriteStartArray();

        if (IsScalarRowType(value.RowType))
        {
            foreach (var row in value.Rows)
                WriteScalar(writer, Normalize(row, null, context.Dialect), value.RowType);
        }
        else
        {
            var columns = BuildColumns(value, context);

            foreach (var row in value.Rows)
            {
                writer.WriteStartObject();

                foreach (var column in columns)
                {
                    writer.WritePropertyName(column.Name);
                    WriteScalar(writer, column.GetValue(row), column.ClrType);
                }

                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    /// <summary>Serializes the parameter as a UTF-8 JSON array and returns it as a string.</summary>
    internal static string WriteJson(TableParameterValue value, DataContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
            WriteJson(value, writer, context);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Builds a typed one-dimensional array from a scalar table parameter, for a provider that binds a
    /// scalar set as a native array (PostgreSQL, ClickHouse). The element type mirrors the row type: a
    /// nullable row type produces a nullable element array so NULL rows survive, a non-nullable one a
    /// non-nullable array; an enum is stored as its underlying number and, on a provider without a
    /// native duration type, a <see cref="TimeSpan"/> as its integer storage value (the same
    /// representation the entity path binds).
    /// </summary>
    internal static Array ToTypedArray(TableParameterValue value, DataContext context)
    {
        var rowType = value.RowType;
        var underlying = Nullable.GetUnderlyingType(rowType) ?? rowType;
        var storage = StorageType(underlying, context.Dialect);

        // Mirror the row type's nullability: a non-nullable row type yields non-nullable elements, a
        // nullable one yields Nullable<T> elements so NULL rows survive. A reference element type is
        // already nullable.
        var elementType = Nullable.GetUnderlyingType(rowType) is not null && storage.IsValueType
            ? typeof(Nullable<>).MakeGenericType(storage)
            : storage;

        var values = new List<object?>();

        foreach (var row in value.Rows)
        {
            if (row is null)
            {
                values.Add(null);
                continue;
            }

            values.Add(ConvertStorage(Normalize(row, null, context.Dialect)!, storage));
        }

        var array = Array.CreateInstance(elementType, values.Count);
        for (var i = 0; i < values.Count; i++)
            array.SetValue(values[i], i);

        return array;
    }

    private static Type StorageType(Type type, ISqlDialect dialect)
    {
        if (type.IsEnum)
            return Enum.GetUnderlyingType(type);

        if (type == typeof(TimeSpan))
            return dialect.SupportsNativeDuration ? typeof(TimeSpan) : typeof(long);

        return type switch
        {
            _ when type == typeof(sbyte) => typeof(short),
            _ when type == typeof(ushort) => typeof(int),
            _ when type == typeof(uint) => typeof(long),
            _ when type == typeof(ulong) => typeof(decimal),
            _ when type == typeof(char) => typeof(string),
            _ => type,
        };
    }

    private static object ConvertStorage(object value, Type target)
    {
        if (target == value.GetType())
            return value;

        return target == typeof(string)
            ? value.ToString()!
            : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes one normalized scalar value as JSON. The value is dispatched to the temporal, text and
    /// numeric writers in turn; a value none of them recognizes throws, preserving the exact output (and
    /// the <c>NaN</c>/<c>±∞</c> rejection) previously produced by a single flat <c>switch</c>.
    /// </summary>
    private static void WriteScalar(Utf8JsonWriter writer, object? value, Type declaredType)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (TryWriteTemporal(writer, value) || TryWriteText(writer, value) || TryWriteNumber(writer, value))
            return;

        throw new NotSupportedException(
            $"Table-valued parameter does not know how to write a value of type {value.GetType().Name} as JSON (declared {declaredType.Name}).");
    }

    /// <summary>Writes a date/time value as an ISO-8601 string; returns whether <paramref name="value"/> was handled.</summary>
    private static bool TryWriteTemporal(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case DateTime dt: writer.WriteStringValue(dt); return true;
            case DateTimeOffset dto: writer.WriteStringValue(dto); return true;
            case DateOnly date: writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); return true;
            case TimeOnly time: writer.WriteStringValue(time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)); return true;
            case TimeSpan duration: writer.WriteStringValue(duration.ToString("c", CultureInfo.InvariantCulture)); return true;
            default: return false;
        }
    }

    /// <summary>Writes a string, boolean, base64 <c>byte[]</c>, <see cref="Guid"/> or <c>char</c>; returns whether it was handled.</summary>
    private static bool TryWriteText(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string s: writer.WriteStringValue(s); return true;
            case bool b: writer.WriteBooleanValue(b); return true;
            case byte[] bytes: writer.WriteBase64StringValue(bytes); return true;
            case Guid guid: writer.WriteStringValue(guid); return true;
            case char c: writer.WriteStringValue(c.ToString()); return true;
            default: return false;
        }
    }

    /// <summary>Writes a decimal or one of the integral/floating numeric types; returns whether it was handled.</summary>
    private static bool TryWriteNumber(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case decimal dec: writer.WriteNumberValue(dec); return true;
            case double dbl:
                ThrowIfNotFinite(dbl);
                writer.WriteNumberValue(dbl);
                return true;
            case float flt:
                ThrowIfNotFinite(flt);
                writer.WriteNumberValue(flt);
                return true;
            case byte by: writer.WriteNumberValue(by); return true;
            case sbyte sb: writer.WriteNumberValue(sb); return true;
            case short sh: writer.WriteNumberValue(sh); return true;
            case ushort us: writer.WriteNumberValue(us); return true;
            case int i: writer.WriteNumberValue(i); return true;
            case uint ui: writer.WriteNumberValue(ui); return true;
            case long lng: writer.WriteNumberValue(lng); return true;
            case ulong ulng: writer.WriteNumberValue(ulng); return true;
            default: return false;
        }
    }

    private static void ThrowIfNotFinite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new NotSupportedException("NaN and Infinity have no JSON representation and cannot be bound as a table-valued parameter value.");
    }

}
