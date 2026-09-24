namespace NextORM.Core;

/// <summary>
/// The unit in which a <see cref="System.TimeSpan"/> property is stored when the provider has no
/// native interval/time-of-day type and the value is kept in an integer column. Providers with a
/// native duration type (PostgreSQL <c>interval</c>, MySQL/MariaDB <c>TIME</c>) ignore the unit and
/// store the value natively; <see cref="Ticks"/> is the unit used when none is declared.
/// </summary>
public enum DurationUnit
{
    /// <summary>100-nanosecond ticks, the <see cref="System.TimeSpan"/> storage unit.</summary>
    Ticks,
    /// <summary>Whole microseconds.</summary>
    Microseconds,
    /// <summary>Whole milliseconds.</summary>
    Milliseconds,
    /// <summary>Whole seconds.</summary>
    Seconds,
    /// <summary>Whole minutes.</summary>
    Minutes,
    /// <summary>Whole hours.</summary>
    Hours,
    /// <summary>Whole days.</summary>
    Days
}
