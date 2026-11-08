using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The single conversion point between a provider's native time-of-day value and a
/// <see cref="TimeOnly"/> projection.
/// <para>
/// A time-of-day column is exposed by the drivers either as a native <see cref="TimeOnly"/> (none today)
/// or as a <see cref="TimeSpan"/> (SQL Server <c>time</c>, PostgreSQL <c>time</c>). This helper accepts
/// exactly those two shapes and nothing else: no modulo, no truncation, no string parsing, no integer
/// reinterpretation and no <see cref="Convert.ChangeType(object, Type)"/>. A value outside
/// <c>[00:00:00, 24:00:00)</c> is a duration, not a time of day, and is reported as such instead of being
/// silently wrapped.
/// </para>
/// <para>
/// The provider policy is the first gate: when <c>refusal</c> is not <see langword="null"/> the helper
/// throws it before looking at the value, so a provider whose reader happens to hand back a
/// <see cref="TimeOnly"/> can never bypass its own refusal.
/// </para>
/// </summary>
internal static class TimeOnlyProjectionReader
{
    /// <summary>
    /// The refusal reported by a context that has no verified native time-of-day reader. Providers that
    /// support the projection override the context policy to <see langword="null"/>; the generic value is
    /// also the safe default for an execution path that cannot reach the context policy.
    /// </summary>
    internal const string GenericRefusal =
        "TimeOnly projection refused for this provider: no verified native time-of-day reader.";

    /// <summary>Reported when a supported provider returns a value a time of day cannot represent.</summary>
    internal const string OutOfDomainMessage =
        "TimeOnly projection value is outside [00:00:00, 24:00:00); use TimeSpan.";

    /// <summary>Reported when a supported provider returns a CLR type that is not a native time value.</summary>
    internal const string AlienValueMessage =
        "TimeOnly projection requires a native TimeOnly or TimeSpan value; use an explicit value converter.";

    /// <summary>
    /// The <see cref="MethodInfo"/> of <see cref="ReadNative(object?, string?)"/>, used by the row-mapper
    /// expression composer to emit the call once per column instead of a per-row boxed conversion.
    /// </summary>
    internal static readonly MethodInfo ReadNativeMethod =
        typeof(TimeOnlyProjectionReader).GetMethod(
            nameof(ReadNative),
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

    /// <summary>
    /// Converts a provider value to a <see cref="TimeOnly"/> under the provider's policy.
    /// </summary>
    /// <param name="value">
    /// The value the provider returned: a native <see cref="TimeOnly"/> or a <see cref="TimeSpan"/>. A
    /// <see langword="null"/>/<see cref="DBNull"/> here means the caller's null handling is wrong (the
    /// mapper and the scalar seam both short-circuit SQL NULL first), so it is rejected rather than
    /// converted into a default time.
    /// </param>
    /// <param name="refusal">
    /// The context's policy message, or <see langword="null"/> when the provider has a verified native
    /// time-of-day reader. Never <see langword="null"/> by accident: the mapper and the scalar seam pass
    /// the context value, and a refusal is thrown even when <paramref name="value"/> is already a
    /// <see cref="TimeOnly"/>.
    /// </param>
    /// <returns>The exact time of day, preserving every tick the provider delivered.</returns>
    /// <exception cref="NotSupportedException">
    /// The provider refuses the projection, the value is outside <c>[00:00:00, 24:00:00)</c>, or the value
    /// is not a native time-of-day representation.
    /// </exception>
    internal static TimeOnly ReadNative(object? value, string? refusal)
    {
        if (refusal is not null)
            throw new NotSupportedException(refusal);

        switch (value)
        {
            case TimeOnly time:
                return time;

            case TimeSpan span:
                if (span.Ticks < 0 || span.Ticks >= TimeSpan.TicksPerDay)
                    throw new NotSupportedException(OutOfDomainMessage);

                return TimeOnly.FromTimeSpan(span);

            default:
                throw new NotSupportedException(AlienValueMessage);
        }
    }
}
