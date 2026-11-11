using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NpgsqlTypes;

namespace NextORM.Postgres.Tests;

/// <summary>
/// DB-free coverage of PostgreSQL <c>interval</c> materialisation into <see cref="TimeSpan"/> (T264).
/// The buffered column mapper is compiled before the reader is open, so the driver type must be
/// discriminated per value from <see cref="IDataRecord.GetDataTypeName"/>: only <c>interval</c> is read
/// as <see cref="NpgsqlInterval"/>, while <c>time</c>/<c>timetz</c> keep the existing accessor.
/// <para>
/// The fake reader reproduces the pinned Npgsql 10.0.3 conversion exactly: a positive month throws,
/// a negative month is silently dropped and the remaining days/time are returned. That negative-month
/// truncation is the defect these tests red-lock.
/// </para>
/// </summary>
public class T264IntervalMaterializationTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    /// <summary>01:02:03.004 — a value used to prove the non-interval <c>time</c> path is untouched.</summary>
    private static readonly TimeSpan Fractional = TimeSpan.FromTicks(37_230_040_000);

    /// <summary>1 day 02:03:04 as the driver reports it: days = 1, time = 7_384_000_000 µs.</summary>
    private const long OneDayTwoHoursTicks = 937_840_000_000L;

    private const string DriverMonthMessage =
        "Cannot read interval values with non-zero months as TimeSpan, since that type doesn't support months. " +
        "Consider using NodaTime Period which better corresponds to PostgreSQL interval, or read the value as " +
        "NpgsqlInterval, or transform the interval to not contain months or years in PostgreSQL before reading it.";

    /// <summary>
    /// A calendar month — either sign — has no lossless <see cref="TimeSpan"/> representation, so it must
    /// be rejected before any duration arithmetic. The negative cases are the red arm: the pinned driver
    /// returns a truncated duration for them instead of throwing.
    /// </summary>
    [Fact]
    public void RejectsNonzeroMonthsBeforeDurationArithmetic()
    {
        var body = Map(typeof(TimeSpan));

        AssertMonthRejected(body, new NpgsqlInterval(months: -1, days: 0, time: 0), "-1 month");
        AssertMonthRejected(body, new NpgsqlInterval(months: -1, days: 2, time: 0), "-1 month + 2 days");
        AssertMonthRejected(body, new NpgsqlInterval(months: 1, days: 0, time: 0), "+1 month");
        AssertMonthRejected(body, new NpgsqlInterval(months: 1, days: 2, time: 0), "+1 month + 2 days");
    }

    /// <summary>
    /// Zero-month intervals convert exactly: days and microseconds only, no 30-day month term, with
    /// zero, negative and sub-second values preserved.
    /// </summary>
    [Theory]
    [InlineData(0, 0L, 0L)]
    [InlineData(1, 7_384_000_000L, OneDayTwoHoursTicks)]
    [InlineData(-3, -7_200_000_000L, -2_664_000_000_000L)]
    [InlineData(0, 1_234_567L, 12_345_670L)]
    [InlineData(-1, 1L, -863_999_999_990L)]
    public void ReadsZeroMonthComponentsExactly(int days, long microseconds, long expectedTicks)
    {
        var body = Map(typeof(TimeSpan));

        Invoke(body, Interval(new NpgsqlInterval(months: 0, days: days, time: microseconds)))
            .Should().Be(TimeSpan.FromTicks(expectedTicks));
    }

    /// <summary>
    /// SQL NULL keeps its existing policy: <c>null</c> for a nullable target, <c>default</c> for
    /// <c>DefaultOnNull</c>, and in both cases no interval value is read.
    /// </summary>
    [Fact]
    public void HonoursNullableAndDefaultOnNull()
    {
        var nullableReader = new FakeIntervalReader("interval", typeof(NpgsqlInterval), value: null);
        Invoke(Map(typeof(TimeSpan?)), nullableReader).Should().BeNull();
        nullableReader.IntervalReads.Should().Be(0, "SQL NULL must not be read as an interval value");

        var defaultReader = new FakeIntervalReader("interval", typeof(NpgsqlInterval), value: null);
        Invoke(Map(typeof(TimeSpan), defaultOnNull: true), defaultReader).Should().Be(TimeSpan.Zero);
        defaultReader.IntervalReads.Should().Be(0, "SQL NULL must not be read as an interval value");

        var presentReader = Interval(new NpgsqlInterval(months: 0, days: 1, time: 7_384_000_000L));
        Invoke(Map(typeof(TimeSpan?)), presentReader).Should().Be(TimeSpan.FromTicks(OneDayTwoHoursTicks));
        presentReader.IntervalReads.Should().Be(1);
    }

    /// <summary>
    /// A <c>time</c> column with a <see cref="TimeSpan"/> target is not an interval: it must keep the
    /// existing driver accessor instead of being probed as <see cref="NpgsqlInterval"/>.
    /// </summary>
    [Fact]
    public void PreservesNonIntervalTimeSpanAccessor()
    {
        var reader = new FakeIntervalReader("time", typeof(TimeSpan), Fractional);

        Invoke(Map(typeof(TimeSpan?)), reader).Should().Be(Fractional);
        reader.IntervalReads.Should().Be(0, "a time value is not a PostgreSQL interval");
        reader.TimeSpanReads.Should().Be(1);
    }

    /// <summary>
    /// A configured converter keeps first precedence: the interval probe never runs and the converter's
    /// result (not the raw duration) is materialised.
    /// </summary>
    [Fact]
    public void PreservesConfiguredConverterPrecedence()
    {
        var converter = new OffsetTimeConverter(TimeSpan.FromHours(5));
        var reader = new FakeIntervalReader(
            "interval",
            typeof(TimeSpan),
            new NpgsqlInterval(months: 1, days: 1, time: 0),
            emulateDriverMonthCheck: false);

        var value = Invoke(Map(typeof(TimeSpan?), converter: converter), reader);

        value.Should().Be(TimeSpan.FromDays(1) + converter.Offset);
        converter.ReadCount.Should().Be(1);
        reader.IntervalReads.Should().Be(0, "the interval probe must not intercept a configured converter");
    }

    /// <summary>
    /// A zero-month interval whose days/time still do not fit <see cref="TimeSpan"/> must overflow
    /// loudly instead of wrapping into a plausible-looking duration.
    /// </summary>
    [Fact]
    public void RejectsUnrepresentableZeroMonthDurations()
    {
        var body = Map(typeof(TimeSpan));

        AssertOverflow(body, new NpgsqlInterval(months: 0, days: 2_000_000_000, time: 0), "days");
        AssertOverflow(body, new NpgsqlInterval(months: 0, days: 0, time: long.MaxValue), "microseconds");
    }

    /// <summary>
    /// One compiled mapper serves successive rows: the month decision is taken from each reader value,
    /// so a cached mapper cannot retain a previous zero-month verdict.
    /// </summary>
    [Fact]
    public void UsesCurrentRowIntervalValue()
    {
        var mapper = Expression.Lambda(Map(typeof(TimeSpan)), Record).Compile();

        mapper.DynamicInvoke(Interval(new NpgsqlInterval(months: 0, days: 1, time: 7_384_000_000L)))
            .Should().Be(TimeSpan.FromTicks(OneDayTwoHoursTicks));

        var (error, value) = TryInvoke(mapper, Interval(new NpgsqlInterval(months: 1, days: 0, time: 0)));
        error.Should().BeOfType<InvalidCastException>(
            $"the second row carries a calendar month; the cached mapper returned {Describe(value)} instead of throwing");

        mapper.DynamicInvoke(Interval(new NpgsqlInterval(months: 0, days: 2, time: 0)))
            .Should().Be(TimeSpan.FromDays(2), "a later zero-month row is still exact");
    }

    private static FakeIntervalReader Interval(NpgsqlInterval value)
        => new("interval", typeof(NpgsqlInterval), value);

    private static Expression Map(Type dest, bool defaultOnNull = false, IPropertyValueConverter? converter = null)
    {
        var column = new SelectExpression(dest)
        {
            Index = 0,
            PropertyName = "V",
            Converter = converter,
        };

        if (defaultOnNull)
        {
            // DefaultOnNull is set internally by the *OrDefault scalar terminals; the property setter is
            // internal to nextorm.core, so drive it from the test via reflection.
            typeof(SelectExpression).GetProperty(nameof(SelectExpression.DefaultOnNull))!.SetValue(column, true);
        }

        return PostgresTestContext.CreatePostgres().MapColumnExpression(column, Record);
    }

    private static object? Invoke(Expression body, IDataRecord reader)
        => Expression.Lambda(body, Record).Compile().DynamicInvoke(reader);

    private static void AssertMonthRejected(Expression body, NpgsqlInterval interval, string description)
    {
        var (error, value) = TryInvoke(body, Interval(interval));

        var exception = error.Should().BeOfType<InvalidCastException>(
            $"{description} has a calendar month and no lossless TimeSpan representation; " +
            $"the mapper returned {Describe(value)} instead of throwing").Which;

        exception.Message.Should().Contain("calendar month");
        exception.Message.Should().Contain("no lossless TimeSpan representation");
    }

    private static void AssertOverflow(Expression body, NpgsqlInterval interval, string component)
    {
        var (error, value) = TryInvoke(body, Interval(interval));

        error.Should().BeOfType<OverflowException>(
            $"the {component} component does not fit a TimeSpan; the mapper returned {Describe(value)} instead of overflowing");
    }

    private static (Exception? Error, object? Value) TryInvoke(Expression body, IDataRecord reader)
        => TryInvoke(Expression.Lambda(body, Record).Compile(), reader);

    private static (Exception? Error, object? Value) TryInvoke(Delegate mapper, IDataRecord reader)
    {
        try
        {
            return (null, mapper.DynamicInvoke(reader));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return (ex.InnerException, null);
        }
    }

    private static string Describe(object? value) => value switch
    {
        null => "null",
        TimeSpan duration => $"'{duration}' ({duration.Ticks} ticks)",
        _ => $"'{value}'",
    };

    /// <summary>
    /// Adds a fixed offset to every provider value so the test can tell the converter's result from the
    /// raw duration, and counts the reads so a double conversion is visible.
    /// </summary>
    private sealed class OffsetTimeConverter(TimeSpan offset) : IPropertyValueConverter
    {
        public TimeSpan Offset { get; } = offset;

        public int ReadCount { get; private set; }

        public Type ProviderType => typeof(TimeSpan);

        public bool ConvertsNulls => false;

        public object? ConvertToProvider(object? model) => model;

        public object? ConvertFromProvider(object? provider)
        {
            ReadCount++;
            return (TimeSpan)provider! + Offset;
        }
    }

    /// <summary>
    /// A one-column reader that reports the PostgreSQL data type name and reproduces the pinned
    /// Npgsql 10.0.3 <c>TimeSpan</c> conversion: a positive month throws, a negative month is dropped.
    /// Typed getters other than <see cref="TimeSpan"/> and <see cref="NpgsqlInterval"/> fail, exactly
    /// like a driver read of the wrong field type.
    /// </summary>
    private sealed class FakeIntervalReader(string dataTypeName, Type fieldType, object? value, bool emulateDriverMonthCheck = true) : DbDataReader
    {
        public int IntervalReads { get; private set; }

        public int TimeSpanReads { get; private set; }

        public int DataTypeNameReads { get; private set; }

        public override int FieldCount => 1;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => -1;

        public override int Depth => 0;

        public override int VisibleFieldCount => 1;

        public override object this[int ordinal] => throw new NotSupportedException();

        public override object this[string name] => throw new NotSupportedException();

        public override string GetDataTypeName(int ordinal)
        {
            DataTypeNameReads++;
            return dataTypeName;
        }

        public override Type GetFieldType(int ordinal) => fieldType;

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override object GetValue(int ordinal) => value ?? DBNull.Value;

        public override T GetFieldValue<T>(int ordinal)
        {
            if (typeof(T) == typeof(NpgsqlInterval))
            {
                IntervalReads++;
                return (T)value!;
            }

            if (typeof(T) == typeof(TimeSpan))
            {
                TimeSpanReads++;
                if (dataTypeName != "interval")
                    return (T)value!;

                var interval = (NpgsqlInterval)value!;
                if (emulateDriverMonthCheck && interval.Months > 0)
                    throw new InvalidCastException(DriverMonthMessage);

                return (T)(object)unchecked(new TimeSpan(
                    interval.Days * TimeSpan.TicksPerDay + interval.Time * TimeSpan.TicksPerMicrosecond));
            }

            throw new InvalidCastException($"GetFieldValue<{typeof(T).Name}> is not supported by the fake reader.");
        }

        public override string GetName(int ordinal) => "V";

        public override int GetOrdinal(string name) => 0;

        public override bool Read() => throw new NotSupportedException();

        public override bool NextResult() => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => throw new NotSupportedException();

        public override int GetValues(object[] values) => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => throw new InvalidCastException();

        public override byte GetByte(int ordinal) => throw new InvalidCastException();

        public override short GetInt16(int ordinal) => throw new InvalidCastException();

        public override int GetInt32(int ordinal) => throw new InvalidCastException();

        public override long GetInt64(int ordinal) => throw new InvalidCastException();

        public override float GetFloat(int ordinal) => throw new InvalidCastException();

        public override double GetDouble(int ordinal) => throw new InvalidCastException();

        public override decimal GetDecimal(int ordinal) => throw new InvalidCastException();

        public override string GetString(int ordinal) => throw new InvalidCastException();

        public override Guid GetGuid(int ordinal) => throw new InvalidCastException();

        public override DateTime GetDateTime(int ordinal) => throw new InvalidCastException();

        public override char GetChar(int ordinal) => throw new InvalidCastException();

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
            => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
            => throw new NotSupportedException();
    }
}
