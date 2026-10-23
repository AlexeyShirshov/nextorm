namespace NextORM.Core;

/// <summary>
/// Expression-derived provider type of a captured parameter. The type comes from the compared
/// column's mapped metadata (or from the captured operand's own declared type when the column side is
/// not a mapped member), never from the runtime value, so a captured SQL <c>NULL</c> binds with the
/// same provider type as every later non-null value. Internal: an engine typing detail, not part of
/// the public contract.
/// </summary>
internal readonly record struct ParameterTypeInfo(System.Data.DbType DbType, byte? Precision, byte? Scale)
{
    /// <summary>
    /// Builds the provider type from a CLR provider type (the converter's provider type or the
    /// nullable-unwrapped column type) plus the declared decimal precision/scale. Returns
    /// <see langword="null"/> for a type the internal mapping does not know, so the parameter keeps
    /// the provider's value-based inference.
    /// </summary>
    internal static ParameterTypeInfo? FromProviderType(Type? providerType, int? precision, int? scale)
    {
        if (providerType is null)
            return null;

        if (providerType.IsEnum)
            providerType = Enum.GetUnderlyingType(providerType);

        if (ToDbType(providerType) is not { } dbType)
            return null;

        return new ParameterTypeInfo(
            dbType,
            precision is > 0 and <= byte.MaxValue ? (byte)precision.Value : null,
            scale is >= 0 and <= byte.MaxValue ? (byte)scale.Value : null);
    }

    private static System.Data.DbType? ToDbType(Type type)
    {
        if (type == typeof(bool)) return System.Data.DbType.Boolean;
        if (type == typeof(byte)) return System.Data.DbType.Byte;
        if (type == typeof(sbyte)) return System.Data.DbType.SByte;
        if (type == typeof(short)) return System.Data.DbType.Int16;
        if (type == typeof(ushort)) return System.Data.DbType.UInt16;
        if (type == typeof(int)) return System.Data.DbType.Int32;
        if (type == typeof(uint)) return System.Data.DbType.UInt32;
        if (type == typeof(long)) return System.Data.DbType.Int64;
        if (type == typeof(ulong)) return System.Data.DbType.UInt64;
        if (type == typeof(float)) return System.Data.DbType.Single;
        if (type == typeof(double)) return System.Data.DbType.Double;
        if (type == typeof(decimal)) return System.Data.DbType.Decimal;
        if (type == typeof(string) || type == typeof(char)) return System.Data.DbType.String;
        if (type == typeof(DateTime)) return System.Data.DbType.DateTime;
        if (type == typeof(DateTimeOffset)) return System.Data.DbType.DateTimeOffset;
        if (type == typeof(TimeSpan)) return System.Data.DbType.Time;
        if (type == typeof(Guid)) return System.Data.DbType.Guid;
        if (type == typeof(byte[])) return System.Data.DbType.Binary;

        return null;
    }
}

/// <summary>
/// Name/value pair for a single command parameter.
/// </summary>
/// <remarks>
/// Renamed from <c>Param</c> (<c>Parameter</c> is the full word). See
/// <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public class Parameter(string name, object? value)
{
    /// <summary>
    /// The placeholder name (without the provider's parameter prefix) used to bind
    /// <see cref="Value"/> in the generated SQL.
    /// </summary>
    public string Name { get; set; } = name;
    /// <summary>
    /// The value bound to <see cref="Name"/>, or <see langword="null"/> for a SQL <c>NULL</c>.
    /// </summary>
    public object? Value { get; set; } = value;

    /// <summary>
    /// True when <see cref="Value"/> is fully determined by the query shape (an inline value list or a
    /// constant-folded expression), so a cached plan does not have to copy it into the command on every
    /// execution. A captured collection/variable is not stable: its value can change while the plan key
    /// stays the same. Internal: this is an engine optimization, not part of the public contract.
    /// </summary>
    internal bool Stable { get; set; }

    /// <summary>
    /// The captured expression this parameter was produced from (a closure member access), or
    /// <see langword="null"/> for a parameter that does not bound a captured value. Used to register a
    /// captured local once per rendered statement, so every occurrence reuses the same placeholder.
    /// Internal: an engine bookkeeping detail, not part of the public contract.
    /// </summary>
    internal ExpressionKey? CapturedKey { get; set; }

    /// <summary>
    /// True when <see cref="Value"/> went through a column converter or a duration-storage
    /// normalization while the parameter was built, so re-reading the raw captured value would not
    /// reproduce <see cref="Value"/>. The immutable guarded parameter-refresh recipe refuses such
    /// parameters and routes them to the original extraction path. Internal: an engine bookkeeping
    /// detail, not part of the public contract.
    /// </summary>
    internal bool HasConversion { get; set; }

    /// <summary>
    /// The expression-derived provider type of this parameter, or <see langword="null"/> when the
    /// parameter keeps the provider's value-based inference. Set only for captured operands of the
    /// approved null-safe comparisons, so a cold captured <c>NULL</c> binds with the compared
    /// column's type and every later value keeps it. Internal: an engine typing detail.
    /// </summary>
    internal System.Data.DbType? DbType { get; init; }

    /// <summary>The decimal precision derived from the compared column's metadata, or <see langword="null"/>.</summary>
    internal byte? Precision { get; init; }

    /// <summary>The decimal scale derived from the compared column's metadata, or <see langword="null"/>.</summary>
    internal byte? Scale { get; init; }
}
