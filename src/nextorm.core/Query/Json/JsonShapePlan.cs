using System.Text.Json;

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
}

/// <summary>
/// One projected column of a JSON stream shape: the JSON property name (already run through the
/// naming policy), the result-set ordinal it is read from and the typed write kind.
/// </summary>
internal readonly struct JsonShapeColumn
{
    /// <summary>Initializes a planned column.</summary>
    internal JsonShapeColumn(int ordinal, string name, Type valueType, JsonWriteKind kind, bool nullable, bool defaultOnNull)
    {
        Ordinal = ordinal;
        Name = name;
        ValueType = valueType;
        Kind = kind;
        Nullable = nullable;
        DefaultOnNull = defaultOnNull;
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
    /// <exception cref="InvalidOperationException">The projection is empty.</exception>
    /// <exception cref="NotSupportedException">A column shape or option combination is not supported.</exception>
    public static JsonShapePlan Build(SelectExpression[]? selectList, bool oneColumn, JsonStreamOptions options, JsonShapeNode? shape = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode is not (JsonStreamMode.Array or JsonStreamMode.NdJson))
            throw new NotSupportedException($"Unknown JsonStreamMode value {options.Mode}; supported modes are Array and NdJson.");

        if (options.Mode == JsonStreamMode.NdJson)
        {
            if (options.Root is not null)
                throw new NotSupportedException("The 'Root' option is only supported in Array mode; NDJSON emits one value per line without a wrapper.");
            if (options.WriteIndented)
                throw new NotSupportedException("The 'WriteIndented' option is only supported in Array mode; NDJSON must not be indented.");
        }

        if (selectList is null || selectList.Length == 0)
        {
            // A captured shape whose construction lowers no scalar columns (for example a nested object
            // with no members) cannot be streamed: the query would select no columns and render invalid
            // SQL. Reject it here with an explicit message instead of the phase-1 "no projection" guard
            // or a provider syntax error.
            if (shape is not null)
                throw new NotSupportedException(
                    "A JSON projection that lowers no scalar columns (for example a nested object with no members) cannot be streamed; give the construction at least one scalar member.");

            throw new InvalidOperationException("JSON streaming requires an explicit Select projection; the query has no selected columns.");
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
                        $"Column '{column.PropertyName}' is value-converted and cannot be streamed as JSON; project the provider representation instead.");

                if (column.IsLobStreaming)
                    throw new NotSupportedException(
                        $"Column '{column.PropertyName}' is a streaming LOB column and cannot be streamed as JSON; project it as a buffered string/byte[] column.");
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
                    $"Column '{column.PropertyName}' is value-converted and cannot be streamed as JSON in phase 1; project the provider representation instead.");

            if (column.IsLobStreaming)
                throw new NotSupportedException(
                    $"Column '{column.PropertyName}' is a streaming LOB column and cannot be streamed as JSON; project it as a buffered string/byte[] column.");

            var (valueType, kind) = Classify(column.PropertyType, column.PropertyName);

            string name;
            if (oneColumn)
            {
                name = column.PropertyName ?? string.Empty;
            }
            else
            {
                if (string.IsNullOrEmpty(column.PropertyName))
                    throw new NotSupportedException(
                        $"Projected column at ordinal {column.Index} has no property name and cannot be written as a JSON object member; use a Select with named members.");

                if (options.PropertyNamingPolicy is not null)
                {
                    name = options.PropertyNamingPolicy.ConvertName(column.PropertyName!);
                    if (name is null)
                        throw new NotSupportedException(
                            $"The property naming policy returned null for column '{column.PropertyName}'; JSON object member names cannot be null.");
                }
                else
                {
                    name = column.PropertyName!;
                }

                if (!names!.Add(name))
                    throw new NotSupportedException(
                        $"Duplicate JSON property name '{name}' after naming-policy conversion; JSON object members must be unique.");
            }

            columns[i] = new JsonShapeColumn(column.Index, name, valueType, kind, column.Nullable, column.DefaultOnNull);
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
                $"The JSON shape leaf '{name}' is bound to reader ordinal {ordinal} but the prepared projection has {columnCount} column(s); the captured shape descriptor does not match the prepared selection.");
    }

    /// <summary>
    /// Maps a declared CLR type to its phase-1 typed write path, unwrapping <see cref="Nullable{T}"/>.
    /// Shared with the recursive shape writer so an array element or a nested scalar leaf uses the exact
    /// same whitelist and never falls back to managed serialization. Throws for an unsupported type.
    /// </summary>
    /// <param name="type">The declared CLR type (may be <see cref="Nullable{T}"/>).</param>
    /// <param name="propertyName">A member/element name used in the rejection message; may be <see langword="null"/>.</param>
    /// <exception cref="NotSupportedException">The type is outside the phase-1 JSON whitelist.</exception>
    internal static (Type ValueType, JsonWriteKind Kind) Classify(Type type, string? propertyName)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;

        if (valueType == typeof(byte) || valueType == typeof(short) || valueType == typeof(int)
            || valueType == typeof(long) || valueType == typeof(float) || valueType == typeof(double)
            || valueType == typeof(decimal))
            return (valueType, JsonWriteKind.Number);

        if (valueType == typeof(bool))
            return (valueType, JsonWriteKind.Boolean);

        if (valueType == typeof(string))
            return (valueType, JsonWriteKind.String);

        if (valueType == typeof(Guid))
            return (valueType, JsonWriteKind.Guid);

        if (valueType == typeof(DateTime))
            return (valueType, JsonWriteKind.DateTime);

        if (valueType == typeof(byte[]))
            return (valueType, JsonWriteKind.Base64);

        throw new NotSupportedException(
            $"Column '{propertyName}' has type {type} which is not supported by JSON streaming in phase 1; supported types are byte, short, int, long, float, double, decimal, bool, string, Guid, DateTime and byte[] (and their Nullable<>).");
    }
}
