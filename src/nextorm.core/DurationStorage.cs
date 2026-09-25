namespace NextORM.Core;

/// <summary>
/// Converts between a <see cref="TimeSpan"/> and the integer representation used by a provider
/// without a native duration type. The conversion is lossless only for <see cref="DurationUnit.Ticks"/>;
/// a coarser unit truncates the sub-unit remainder, which is the same behaviour as storing the value
/// in a database column of that unit.
/// </summary>
internal static class DurationStorage
{
    /// <summary>
    /// The unit in which a duration value is stored: <c>null</c> when the provider keeps it natively
    /// (PostgreSQL <c>interval</c>, MySQL/MariaDB <c>TIME</c>), otherwise the declared unit or
    /// <see cref="DurationUnit.Ticks"/> when none is declared.
    /// </summary>
    internal static DurationUnit? ResolveStorageUnit(IPropertyMetadata? property, ISqlDialect dialect)
        => dialect.SupportsNativeDuration ? null : property?.DurationUnit ?? DurationUnit.Ticks;

    /// <summary>
    /// Converts a duration to the stored integer value in <paramref name="unit"/>.
    /// </summary>
    /// <param name="value">The duration to store.</param>
    /// <param name="unit">The storage unit.</param>
    /// <returns>The value expressed in <paramref name="unit"/>, truncated toward zero.</returns>
    internal static long ToStorage(TimeSpan value, DurationUnit unit)
        => value.Ticks / TicksPerUnit(unit);

    /// <summary>
    /// Converts a stored integer value in <paramref name="unit"/> back to a duration.
    /// </summary>
    /// <param name="value">The stored integer value.</param>
    /// <param name="unit">The storage unit.</param>
    /// <returns>The reconstructed duration.</returns>
    internal static TimeSpan FromStorage(long value, DurationUnit unit)
        => new(value * TicksPerUnit(unit));

    private static long TicksPerUnit(DurationUnit unit) => unit switch
    {
        DurationUnit.Ticks => 1L,
        DurationUnit.Microseconds => TimeSpan.TicksPerMicrosecond,
        DurationUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
        DurationUnit.Seconds => TimeSpan.TicksPerSecond,
        DurationUnit.Minutes => TimeSpan.TicksPerMinute,
        DurationUnit.Hours => TimeSpan.TicksPerHour,
        DurationUnit.Days => TimeSpan.TicksPerDay,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown duration unit.")
    };

    /// <summary>
    /// Normalizes a write value: a <see cref="TimeSpan"/> stored natively is left untouched, an
    /// integer-stored one is converted to its declared unit. Every other value passes through.
    /// </summary>
    /// <param name="value">The value being written.</param>
    /// <param name="property">The mapped property the value targets, or <c>null</c>.</param>
    /// <param name="dialect">The dialect rendering the statement.</param>
    /// <returns>The value to pass to the provider parameter.</returns>
    internal static object? ToParameterValue(object? value, IPropertyMetadata? property, ISqlDialect dialect)
    {
        if (value is TimeSpan duration && ResolveStorageUnit(property, dialect) is { } unit)
            return ToStorage(duration, unit);

        return value;
    }
}
