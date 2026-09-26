namespace NextORM.Core;

/// <summary>
/// Validates a declared decimal precision/scale pair against the type that is actually bound for the
/// column. The domain is provider-neutral and bounded by SQL Server's maximum (precision 1..38, scale
/// 0..precision), so every provider sees the same rules and an out-of-range pair fails early instead of
/// reaching a provider's column metadata. The pair is only meaningful when the <b>bound provider type</b>
/// is <see cref="decimal"/>: a non-decimal model mapped through a value converter to decimal is
/// accepted, while a decimal model mapped to a non-decimal representation (a converter to
/// <see cref="string"/> or a JSON column) is rejected rather than silently ignored.
/// </summary>
internal static class DecimalPrecisionRules
{
    /// <summary>The maximum supported precision (SQL Server's <c>decimal(38, s)</c>).</summary>
    internal const int MaxPrecision = 38;

    /// <summary>
    /// Validates a declared precision/scale pair for a column whose bound provider type is
    /// <paramref name="boundType"/> (the converter's provider type when one is mapped, otherwise the
    /// property type, after the enum/duration reduction). A pair that is entirely absent (both
    /// <see langword="null"/>) is valid and means the provider default; a partially declared pair is not.
    /// </summary>
    /// <param name="boundType">The provider type bound for the column.</param>
    /// <param name="propertyName">The property name, used in the error message.</param>
    /// <param name="precision">The declared precision, or <see langword="null"/>.</param>
    /// <param name="scale">The declared scale, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">Exactly one of the pair is declared, or the bound provider type is not decimal.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside 1..38, the scale is negative, or the scale exceeds the precision.</exception>
    internal static void Validate(Type boundType, string propertyName, int? precision, int? scale)
    {
        if (precision is null && scale is null)
            return;

        var underlying = Nullable.GetUnderlyingType(boundType) ?? boundType;
        if (underlying != typeof(decimal))
            throw new InvalidOperationException(
                $"Property '{propertyName}' declares a decimal precision/scale, but its bound column type {boundType} is not decimal.");

        if (precision is null || scale is null)
            throw new InvalidOperationException(
                $"Property '{propertyName}' must declare both a decimal precision and a scale; a partial precision/scale pair is not supported.");

        var declaredPrecision = precision.Value;
        if (declaredPrecision < 1 || declaredPrecision > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), declaredPrecision,
                $"Property '{propertyName}' decimal precision must be between 1 and {MaxPrecision}.");

        var declaredScale = scale.Value;
        if (declaredScale < 0)
            throw new ArgumentOutOfRangeException(nameof(scale), declaredScale,
                $"Property '{propertyName}' decimal scale must not be negative.");

        if (declaredScale > declaredPrecision)
            throw new ArgumentOutOfRangeException(nameof(scale), declaredScale,
                $"Property '{propertyName}' decimal scale {declaredScale} must not exceed the precision {declaredPrecision}.");
    }
}
