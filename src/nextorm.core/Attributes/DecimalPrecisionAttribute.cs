namespace NextORM.Core;

/// <summary>
/// Declares the decimal precision and scale of a mapped <see cref="decimal"/> property. The pair is
/// read by <see cref="IPropertyMetadata"/> (<see cref="IPropertyMetadata.DecimalPrecision"/> and
/// <see cref="IPropertyMetadata.DecimalScale"/>) and used where a provider needs the declared
/// precision/scale, such as the column metadata of a table-valued parameter.
/// <para>
/// The declared domain is provider-neutral and bounded by SQL Server's maximum: precision must be
/// between 1 and 38, and scale between 0 and the precision. A provider without a native decimal
/// precision (for example ClickHouse's <c>Decimal(P, S)</c>) honours the same pair; a provider that
/// does not need it keeps its default. The pair is validated against the type actually bound for the
/// column: a non-<see cref="decimal"/> model mapped through a value converter to decimal is accepted,
/// while a <see cref="decimal"/> model mapped to a non-decimal representation (a converter to
/// <see cref="string"/> or a JSON column) is rejected. An out-of-range pair throws
/// <see cref="ArgumentOutOfRangeException"/>; a pair declared on a non-decimal bound type or a
/// partially declared pair throws <see cref="InvalidOperationException"/>.
/// </para>
/// </summary>
/// <remarks>
/// The equivalent fluent mapping is
/// <see cref="EntityPropertyBuilder{T}.DecimalPrecision(int, int)"/>. The attribute affects only the
/// declared column metadata; nextorm does not generate DDL, so the column must already have a matching
/// precision/scale (the equivalent of <c>HasColumnType("decimal(12,4)")</c> in EF Core).
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DecimalPrecisionAttribute : Attribute
{
    /// <summary>
    /// Creates an attribute declaring <paramref name="precision"/> and <paramref name="scale"/>.
    /// </summary>
    /// <param name="precision">The total number of decimal digits (1 to 38).</param>
    /// <param name="scale">The number of digits to the right of the decimal point (0 to <paramref name="precision"/>).</param>
    public DecimalPrecisionAttribute(int precision, int scale)
    {
        Precision = precision;
        Scale = scale;
    }

    /// <summary>The total number of decimal digits (1 to 38).</summary>
    public int Precision { get; set; }

    /// <summary>The number of digits to the right of the decimal point (0 to <see cref="Precision"/>).</summary>
    public int Scale { get; set; }
}
