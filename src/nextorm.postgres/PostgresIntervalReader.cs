using System.Data;
using System.Data.Common;
using NpgsqlTypes;

namespace NextORM.Postgres;

/// <summary>
/// Converts a PostgreSQL <c>interval</c> value read from a data record into <see cref="TimeSpan"/>
/// (T264). A calendar month has no lossless <see cref="TimeSpan"/> representation — its length varies
/// by month — so any nonzero month component is rejected instead of being truncated or approximated
/// as 30 days; the remaining days and microseconds are converted with checked arithmetic.
/// </summary>
internal static class PostgresIntervalReader
{
    /// <summary>The PostgreSQL data type name the mapper discriminates before reading an interval.</summary>
    internal const string DataTypeName = "interval";

    /// <summary>
    /// Reads the <c>interval</c> at <paramref name="index"/> as the driver's raw interval and converts
    /// it to a duration.
    /// </summary>
    /// <param name="reader">The reader whose current row is materialised.</param>
    /// <param name="index">The zero-based column ordinal.</param>
    /// <returns>The exact duration of a zero-month interval.</returns>
    /// <exception cref="InvalidCastException">The interval carries calendar months.</exception>
    internal static TimeSpan Read(IDataRecord reader, int index)
        => ToTimeSpan(((DbDataReader)reader).GetFieldValue<NpgsqlInterval>(index));

    /// <summary>
    /// Converts a zero-month interval to <see cref="TimeSpan"/>: days plus microseconds, with checked
    /// arithmetic so an unrepresentable value overflows instead of wrapping.
    /// </summary>
    /// <param name="interval">The driver's raw interval value.</param>
    /// <returns>The exact duration of a zero-month interval.</returns>
    /// <exception cref="InvalidCastException">The interval carries calendar months.</exception>
    /// <exception cref="OverflowException">The days/time components do not fit a <see cref="TimeSpan"/>.</exception>
    internal static TimeSpan ToTimeSpan(NpgsqlInterval interval)
    {
        if (interval.Months != 0)
            throw new InvalidCastException(MonthMessage(interval.Months));

        return new TimeSpan(checked(interval.Days * TimeSpan.TicksPerDay + interval.Time * TimeSpan.TicksPerMicrosecond));
    }

    private static string MonthMessage(int months) =>
        $"Cannot materialize a PostgreSQL interval as TimeSpan: the value carries {months} calendar month(s), " +
        "and a calendar month has no lossless TimeSpan representation because its length varies. Remove " +
        "months/years in SQL (project the days and time components, or normalize an interval of days and " +
        "hours) or configure a value converter for the column.";
}
