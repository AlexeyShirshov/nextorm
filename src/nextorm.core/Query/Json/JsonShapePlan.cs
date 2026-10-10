using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextORM.Core;

/// <summary>
/// How a projected column's value is written to JSON. Only the phase-1 scalar whitelist is
/// represented; anything else is rejected while the plan is built (fail-fast, never a managed
/// serialization fallback).
/// </summary>
internal enum JsonWriteKind
{
    /// <summary>A JSON number (<c>byte</c>/<c>short</c>/<c>int</c>/<c>long</c>/<c>float</c>/<c>double</c>/<c>decimal</c>).</summary>
    Number,

    /// <summary>A JSON boolean.</summary>
    Boolean,

    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A JSON string holding the canonical GUID text.</summary>
    Guid,

    /// <summary>A JSON string holding the ISO-8601 timestamp.</summary>
    DateTime,

    /// <summary>A JSON string holding the base64 encoding of a <c>byte[]</c> value.</summary>
    Base64,

    /// <summary>
    /// A JSON string produced by a supported STJ string-enum converter
    /// (<see cref="JsonStringEnumConverter"/> or <see cref="JsonStringEnumConverter{TEnum}"/>).
    /// </summary>
    EnumString,
}

/// <summary>
/// One projected column of a JSON stream shape: the JSON property name (already run through the
/// naming policy), the result-set ordinal it is read from and the typed write kind.
/// </summary>
internal readonly struct JsonShapeColumn
{
    /// <summary>Initializes a planned column.</summary>
    internal JsonShapeColumn(int ordinal, string name, Type valueType, JsonWriteKind kind, bool nullable, bool defaultOnNull,
        Type? enumUnderlyingType = null, JsonConverter? enumStringConverter = null, Type? declaredType = null,
        Type? providerType = null, bool isDirectPassThrough = false)
    {
        Ordinal = ordinal;
        Name = name;
        ValueType = valueType;
        Kind = kind;
        Nullable = nullable;
        DefaultOnNull = defaultOnNull;
        EnumUnderlyingType = enumUnderlyingType;
        EnumStringConverter = enumStringConverter;
        DeclaredType = declaredType;
        ProviderType = providerType;
        IsDirectPassThrough = isDirectPassThrough;
    }

    /// <summary>The zero-based result-set ordinal the value is read from.</summary>
    public int Ordinal { get; }

    /// <summary>The JSON property name (already naming-policy converted).</summary>
    public string Name { get; }

    /// <summary>The non-nullable CLR type of the value (<see cref="Nullable{T}"/> unwrapped).</summary>
    public Type ValueType { get; }

    /// <summary>The typed JSON write path.</summary>
    public JsonWriteKind Kind { get; }

    /// <summary>Whether the projected column can be SQL NULL.</summary>
    public bool Nullable { get; }

    /// <summary>
    /// Whether a SQL NULL means "no row" (a <c>*OrDefault</c> non-nullable scalar projection), so the
    /// writer must emit <c>default(T)</c> instead of JSON <c>null</c>. Mirrors
    /// <c>RowMapperFactory</c>'s substitution.
    /// </summary>
    public bool DefaultOnNull { get; }

    /// <summary>
    /// The underlying integral type of an enum projection, used to read the provider's numeric value;
    /// <see langword="null"/> for a non-enum column. A numeric enum uses <see cref="ValueType"/> as the
    /// underlying type directly, so this is set only for <see cref="JsonWriteKind.EnumString"/>.
    /// </summary>
    public Type? EnumUnderlyingType { get; }

    /// <summary>
    /// The pre-built STJ converter for an enum projected as a string (a supported
    /// <see cref="JsonStringEnumConverter"/> attribute); <see langword="null"/> otherwise. It is created
    /// once while the shape is planned, never resolved per row.
    /// </summary>
    public JsonConverter? EnumStringConverter { get; }

    /// <summary>
    /// The declared CLR type of the projection before <see cref="JsonWriteKind"/> classification (for a
    /// numeric enum this is the enum type, while <see cref="ValueType"/> is its underlying integral type).
    /// Used by the native JSON eligibility check to keep every enum on the managed path even though a
    /// numeric enum is classified as <see cref="JsonWriteKind.Number"/>. <see langword="null"/> for a
    /// column built by the recursive writer that does not carry provenance.
    /// </summary>
    public Type? DeclaredType { get; }

    /// <summary>
    /// The provider-side storage type a value converter reads and writes, or <see langword="null"/> when
    /// the column is read as its CLR type. The managed writer reads this type and converts it to
    /// <see cref="ValueType"/>; the native <c>FOR JSON</c> transport would emit the raw provider value, so
    /// a non-<see langword="null"/> type other than <see cref="ValueType"/> keeps the column managed.
    /// </summary>
    public Type? ProviderType { get; }

    /// <summary>
    /// Whether the column is a proven direct pass-through: a mapped CLR member (or expanded entity
    /// column) whose storage type is read as <see cref="ValueType"/> with no computed/raw accessor. The
    /// managed numeric reader inspects the runtime provider field type and narrows/converts it; the
    /// native transport cannot, so only a proven pass-through may be served from the database document.
    /// </summary>
    public bool IsDirectPassThrough { get; }
}

/// <summary>
/// The frozen shape of a JSON stream: the per-column plan plus the container options. Built once per
/// (already prepared) query shape; the row writer is compiled from it. Unsupported projections and
/// invalid option combinations throw here, before any output is produced.
/// </summary>
internal sealed class JsonShapePlan
{
    private JsonShapePlan(bool isScalar, JsonShapeColumn[] columns, JsonStreamOptions options, JsonShapeNode? shape)
    {
        IsScalar = isScalar;
        Columns = columns;
        Options = options;
        Shape = shape;
    }

    /// <summary>
    /// Shared serializer options handed to a pre-built string-enum converter. The converter instance owns
    /// the naming/allow-integer policy (from its attribute), so these options are only a write-time
    /// parameter and are never consulted per row.
    /// </summary>
    internal static readonly JsonSerializerOptions EnumConverterOptions = new();

    /// <summary>Whether the projection is a single scalar value rather than an object.</summary>
    public bool IsScalar { get; }

    /// <summary>The planned columns, in result-set order.</summary>
    public JsonShapeColumn[] Columns { get; }

    /// <summary>The validated container options.</summary>
    public JsonStreamOptions Options { get; }

    /// <summary>
    /// The recursively captured JSON projection shape, or <see langword="null"/> for a flat phase-1 shape
    /// whose plan is derived directly from the prepared select list. A non-flat shape is consumed by the
    /// phase-2 recursive writer; the phase-1 flat writer rejects it before producing output.
    /// </summary>
    public JsonShapeNode? Shape { get; }

    /// <summary>
    /// Builds the shape plan from a prepared command's projection, validating the type whitelist and
    /// the option combinations. The caller owns the prepared command; no execution happens here.
    /// </summary>
    /// <param name="selectList">The prepared projection, in result-set order.</param>
    /// <param name="oneColumn">Whether the projection is a single scalar value.</param>
    /// <param name="options">The requested container shape.</param>
    /// <param name="shape">
    /// The recursively captured JSON shape when the JSON-only preparer took over (a nested construction,
    /// array member or expanded entity item); <see langword="null"/> for a flat phase-1 projection whose
    /// plan is derived directly from <paramref name="selectList"/>.
    /// </param>
    /// <exception cref="NotSupportedException">The projection is empty or a column shape or option combination is not supported.</exception>
    public static JsonShapePlan Build(SelectExpression[]? selectList, bool oneColumn, JsonStreamOptions options, JsonShapeNode? shape = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode is not (JsonStreamMode.Array or JsonStreamMode.NdJson))
            throw new NotSupportedException($"JSON streaming validation [mode-options]: Unknown JsonStreamMode value {options.Mode}; supported modes are Array and NdJson.");

        if (options.Mode == JsonStreamMode.NdJson)
        {
            if (options.Root is not null)
                throw new NotSupportedException("JSON streaming validation [mode-options]: The 'Root' option is only supported in Array mode; NDJSON emits one value per line without a wrapper.");
            if (options.WriteIndented)
                throw new NotSupportedException("JSON streaming validation [mode-options]: The 'WriteIndented' option is only supported in Array mode; NDJSON must not be indented.");
        }

        if (selectList is null || selectList.Length == 0)
        {
            // A captured shape whose construction lowers no scalar columns (for example a nested object
            // with no members) cannot be streamed: the query would select no columns and render invalid
            // SQL. Reject it here with an explicit message instead of the phase-1 "no projection" guard
            // or a provider syntax error.
            if (shape is not null)
                throw new NotSupportedException(
                    "JSON streaming validation [projection]: A JSON projection that lowers no scalar columns (for example a nested object with no members) cannot be streamed; give the construction at least one scalar member.");

            throw new NotSupportedException("JSON streaming validation [projection]: the query has no selected columns.");
        }

        // A captured shape owns its own (scoped) name validation and recursive writing, so the phase-1 flat
        // column plan is not built for it; the recursive writer consumes the descriptor directly.
        if (shape is not null)
        {
            // The lowered scalar columns must still satisfy the phase-1 fail-closed guards: a value-converted
            // column or a streaming LOB cannot be written through the recursive writer either.
            for (var i = 0; i < selectList.Length; i++)
            {
                var column = selectList[i];

                if (column.Converter is not null)
                    throw new NotSupportedException(
                        $"JSON streaming validation [unsupported-column]: Column '{column.PropertyName}' is value-converted and cannot be streamed as JSON; project the provider representation instead.");

                if (column.IsLobStreaming)
                    throw new NotSupportedException(
                        $"JSON streaming validation [unsupported-column]: Column '{column.PropertyName}' is a streaming LOB column and cannot be streamed as JSON; project it as a buffered string/byte[] column.");
            }

            // Fail closed at prepare time when the descriptor does not match the prepared selection,
            // instead of surfacing an IndexOutOfRangeException on the first row read.
            ValidateShapeOrdinals(shape, selectList.Length);

            return new JsonShapePlan(oneColumn, [], options, shape);
        }

        // Object members must have unique resolved names; detect collisions before any output.
        var names = oneColumn ? null : new HashSet<string>(StringComparer.Ordinal);
        var columns = new JsonShapeColumn[selectList.Length];
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];

            if (column.Converter is not null)
                throw new NotSupportedException(
                    $"JSON streaming validation [unsupported-column]: Column '{column.PropertyName}' is value-converted and cannot be streamed as JSON in phase 1; project the provider representation instead.");

            if (column.IsLobStreaming)
                throw new NotSupportedException(
                    $"JSON streaming validation [unsupported-column]: Column '{column.PropertyName}' is a streaming LOB column and cannot be streamed as JSON; project it as a buffered string/byte[] column.");

            var (valueType, kind, enumUnderlying, enumConverter) = Classify(column.PropertyType, column.PropertyName, ResolveMemberProvenance(column));

            string name;
            if (oneColumn)
            {
                name = column.PropertyName ?? string.Empty;
            }
            else
            {
                if (string.IsNullOrEmpty(column.PropertyName))
                    throw new NotSupportedException(
                        $"JSON streaming validation [names]: Projected column at ordinal {column.Index} has no property name and cannot be written as a JSON object member; use a Select with named members.");

                if (options.PropertyNamingPolicy is not null)
                {
                    name = options.PropertyNamingPolicy.ConvertName(column.PropertyName!);
                    if (name is null)
                        throw new NotSupportedException(
                            $"JSON streaming validation [names]: The property naming policy returned null for column '{column.PropertyName}'; JSON object member names cannot be null.");
                }
                else
                {
                    name = column.PropertyName!;
                }

                if (!names!.Add(name))
                    throw new NotSupportedException(
                        $"JSON streaming validation [names]: Duplicate JSON property name '{name}' after naming-policy conversion; JSON object members must be unique.");
            }

            columns[i] = new JsonShapeColumn(column.Index, name, valueType, kind, column.Nullable, column.DefaultOnNull, enumUnderlying, enumConverter, column.PropertyType, column.ProviderType, IsDirectProjection(column));
        }

        return new JsonShapePlan(oneColumn, columns, options, shape: null);
    }

    /// <summary>
    /// Validates that every ordinal referenced by a captured shape descriptor lies within the prepared
    /// selection. This is a prepare-time fail-closed guard: a descriptor/selection mismatch otherwise
    /// surfaces only on the first row read as an <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    private static void ValidateShapeOrdinals(JsonShapeNode node, int columnCount)
    {
        if (node.Binding is { } binding)
            EnsureOrdinal(binding.Ordinal, columnCount, node.Name);

        switch (node.Kind)
        {
            case JsonShapeNodeKind.Object:
                for (var i = 0; i < node.Members.Length; i++)
                    ValidateShapeOrdinals(node.Members[i], columnCount);

                if (node.Presence.Kind == JsonShapePresenceKind.AnyColumnNotNull)
                    for (var i = 0; i < node.Presence.Ordinals.Length; i++)
                        EnsureOrdinal(node.Presence.Ordinals[i], columnCount, node.Name);
                break;

            case JsonShapeNodeKind.Array:
                if (node.Element is not null)
                    ValidateShapeOrdinals(node.Element, columnCount);
                break;
        }
    }

    private static void EnsureOrdinal(int ordinal, int columnCount, string? name)
    {
        if (ordinal < 0 || ordinal >= columnCount)
            throw new NotSupportedException(
                $"JSON streaming validation [unsupported-column]: The JSON shape leaf '{name}' is bound to reader ordinal {ordinal} but the prepared projection has {columnCount} column(s); the captured shape descriptor does not match the prepared selection.");
    }

    /// <summary>
    /// Resolves the CLR member a projected flat column came from, so an enum projection can honour an
    /// STJ <c>[JsonConverter]</c> attribute. A whole-entity / expanded-entity column carries
    /// <see cref="SelectExpression.PropertyInfo"/>; an anonymous or scalar projection carries the source
    /// member read in <see cref="SelectExpression.Expression"/> (the ordinary preparer does not set
    /// <c>PropertyInfo</c> for those). Returns <see langword="null"/> for a computed column.
    /// </summary>
    private static MemberInfo? ResolveMemberProvenance(SelectExpression column)
    {
        if (column.PropertyInfo is { } property)
            return property;

        var expression = column.Expression;
        if (expression is null)
            return null;

        if (expression is LambdaExpression lambda)
            expression = lambda.Body;

        expression = TypeFacts.UnwrapConvert(expression);
        return expression is MemberExpression member ? member.Member : null;
    }

    /// <summary>
    /// Whether a flat column is a proven direct pass-through: a mapped CLR member (or an expanded
    /// entity column that carries <see cref="SelectExpression.PropertyInfo"/>) rather than a raw
    /// named-table accessor (<c>TableAlias.GetXxx</c> / <c>TableColumn.AsXxx</c>) or a computed
    /// expression. A raw accessor's storage type is not part of the CLR projection, so the managed reader
    /// infers it at read time; the database <c>FOR JSON</c> serializer cannot, and admitting the column
    /// natively could emit a wider/different value than the managed writer.
    /// </summary>
    private static bool IsDirectProjection(SelectExpression column)
    {
        if (column.PropertyInfo is not null)
            return true;

        var expression = column.Expression;
        if (expression is null)
            return false;

        if (expression is LambdaExpression lambda)
            expression = lambda.Body;

        expression = TypeFacts.UnwrapConvert(expression);
        return expression switch
        {
            MemberExpression member => member.Member.DeclaringType != typeof(TableColumn),
            MethodCallExpression call => call.Method.DeclaringType != typeof(TableAlias)
                && call.Method.DeclaringType != typeof(TableColumn),
            _ => false,
        };
    }

    /// <summary>
    /// Maps a declared CLR type to its typed write path, unwrapping <see cref="Nullable{T}"/>. An enum is
    /// numeric by default (its underlying integral type is the write type); a property/type-level stock
    /// <see cref="JsonStringEnumConverter"/> attribute selects the string representation instead. Shared
    /// with the recursive shape writer so an array element or a nested scalar leaf uses the exact same
    /// whitelist and never falls back to managed serialization. Throws for an unsupported type or an
    /// unsupported JSON converter attribute.
    /// </summary>
    /// <param name="type">The declared CLR type (may be <see cref="Nullable{T}"/>).</param>
    /// <param name="propertyName">A member/element name used in the rejection message; may be <see langword="null"/>.</param>
    /// <param name="member">
    /// The source member of a flat projection, or the destination member of a captured member-init, so a
    /// property-level <c>[JsonConverter]</c> is honoured ahead of the enum type attribute.
    /// </param>
    /// <exception cref="NotSupportedException">The type is outside the whitelist or carries an unsupported converter.</exception>
    internal static (Type ValueType, JsonWriteKind Kind, Type? EnumUnderlyingType, JsonConverter? EnumStringConverter) Classify(Type type, string? propertyName, MemberInfo? member)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;

        if (valueType.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(valueType);
            var attribute = ResolveConverterAttribute(member, valueType);
            if (attribute is null)
                return (underlying, JsonWriteKind.Number, null, null);

            var converterType = attribute.ConverterType;
            if (converterType is not null && TryCreateStringEnumConverter(converterType, valueType, out var converter))
                return (valueType, JsonWriteKind.EnumString, underlying, converter);

            throw new NotSupportedException(
                $"JSON streaming validation [unsupported-column]: Column '{propertyName}' has type {valueType} with the JSON converter '{attribute.ConverterType}', which is not supported by JSON streaming; only System.Text.Json.Serialization.JsonStringEnumConverter or JsonStringEnumConverter<TEnum> is supported for an enum projection, or remove the converter to stream the numeric value.");
        }

        // A JSON converter attribute on a non-enum scalar is not a supported streaming shape: the writer
        // has no general converter path, so the attribute must be rejected at plan time instead of being
        // silently ignored (which would stream the undeclared raw value). This mirrors the enum case's
        // fail-fast policy; the only allowed JSON converter is the stock string-enum form on an enum.
        if (ResolveConverterAttribute(member, valueType) is { } nonEnumAttribute)
            throw new NotSupportedException(
                $"JSON streaming validation [unsupported-column]: Column '{propertyName}' has type {valueType} with the JSON converter '{nonEnumAttribute.ConverterType}', which is not supported by JSON streaming; the only supported JSON converter attribute is System.Text.Json.Serialization.JsonStringEnumConverter or JsonStringEnumConverter<TEnum> on an enum projection.");

        if (valueType == typeof(byte) || valueType == typeof(short) || valueType == typeof(int)
            || valueType == typeof(long) || valueType == typeof(float) || valueType == typeof(double)
            || valueType == typeof(decimal))
            return (valueType, JsonWriteKind.Number, null, null);

        if (valueType == typeof(bool))
            return (valueType, JsonWriteKind.Boolean, null, null);

        if (valueType == typeof(string))
            return (valueType, JsonWriteKind.String, null, null);

        if (valueType == typeof(Guid))
            return (valueType, JsonWriteKind.Guid, null, null);

        if (valueType == typeof(DateTime))
            return (valueType, JsonWriteKind.DateTime, null, null);

        if (valueType == typeof(byte[]))
            return (valueType, JsonWriteKind.Base64, null, null);

        throw new NotSupportedException(
            $"JSON streaming validation [unsupported-column]: Column '{propertyName}' has type {type} which is not supported by JSON streaming; supported types are byte, short, int, long, float, double, decimal, bool, string, Guid, DateTime and byte[] (and their Nullable<>) and enum.");
    }

    /// <summary>
    /// Resolves the effective JSON converter attribute: the member attribute wins over the type
    /// attribute (matching STJ precedence). Used for the enum string-enum form and to fail fast on any
    /// other (unsupported) converter attribute on a scalar projection.
    /// </summary>
    private static JsonConverterAttribute? ResolveConverterAttribute(MemberInfo? member, Type type)
    {
        if (member is not null && member.GetCustomAttribute<JsonConverterAttribute>() is { } memberAttribute)
            return memberAttribute;

        return type.GetCustomAttribute<JsonConverterAttribute>();
    }

    /// <summary>
    /// Recognizes the stock STJ string-enum converter forms and builds the concrete converter once.
    /// Any other converter type (including a custom enum converter) is rejected by the caller.
    /// </summary>
    private static bool TryCreateStringEnumConverter(Type converterType, Type enumType, out JsonConverter converter)
    {
        var factoryType = converterType == typeof(JsonStringEnumConverter)
            ? typeof(JsonStringEnumConverter)
            : converterType.IsGenericType
                && converterType.GetGenericTypeDefinition() == typeof(JsonStringEnumConverter<>)
                && converterType.GetGenericArguments()[0] == enumType
                ? converterType
                : null;

        if (factoryType is null)
        {
            converter = null!;
            return false;
        }

        var factory = factoryType == typeof(JsonStringEnumConverter)
            ? (JsonConverterFactory)new JsonStringEnumConverter()
            : (JsonConverterFactory)Activator.CreateInstance(factoryType)!;

        converter = factory.CreateConverter(enumType, EnumConverterOptions)
            ?? throw new NotSupportedException(
                $"JSON streaming validation [unsupported-column]: The JSON converter '{converterType}' did not produce a converter for enum {enumType}.");

        return true;
    }
}
