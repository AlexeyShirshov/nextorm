using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// A minimal entity for the DB-free by-name column projection smoke (T259 R18): its only declared
/// member is the key, so the <c>col</c> column is reached exclusively through
/// <see cref="SqlFunctions.Column{T}"/>. No database is touched.
/// </summary>
[SqlTable("t259_time_entity")]
internal interface ITimeEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

/// <summary>
/// DB-free coverage of SQL Server <c>time</c> materialisation (T259): the buffered column mapper
/// (<see cref="SqlServerDataContext.MapColumnExpression"/>) is compiled before the reader is open, so
/// it must discriminate the driver's field type per invocation. SqlClient returns a boxed
/// <see cref="TimeSpan"/> for <c>time</c>/<c>timefromparts</c> while a mapped duration property stays
/// <c>bigint</c>, and both reach the same mapper.
/// <para>
/// The fake reader declares one storage type: typed getters throw when they do not match it, while
/// <see cref="IDataRecord.GetValue"/> returns the boxed value — exactly the split the real driver
/// creates (a native <see cref="TimeSpan"/> is not <see cref="IConvertible"/>, an integer is). The
/// tests never open a connection.
/// </para>
/// </summary>
public class SqlServerTimeSpanMappingTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    /// <summary>01:02:03.004 — the issue's <c>timefromparts(1, 2, 3, 4, 3)</c> value.</summary>
    private static readonly TimeSpan Fractional = TimeSpan.FromTicks(37_230_040_000);

    /// <summary>Zero, a fractional <c>time(3)</c> value, the 100 ns tick of <c>time(7)</c>, and the last tick before 24 h.</summary>
    public static TheoryData<TimeSpan> NativeTimeCases() => new()
    {
        { TimeSpan.Zero },
        { new TimeSpan(100) },
        { Fractional },
        { TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1) },
    };

    /// <summary>Every declared <see cref="DurationUnit"/>: a native time value must ignore the storage unit.</summary>
    public static TheoryData<DurationUnit> DurationUnits() => new()
    {
        { DurationUnit.Ticks },
        { DurationUnit.Microseconds },
        { DurationUnit.Milliseconds },
        { DurationUnit.Seconds },
        { DurationUnit.Minutes },
        { DurationUnit.Hours },
        { DurationUnit.Days },
    };

    /// <summary>
    /// Stored bigint durations for every unit, including zero, a negative value and a value at or
    /// beyond 24 h; each stays inside the representable range after unit scaling.
    /// </summary>
    public static TheoryData<long, DurationUnit> StoredDurationCases() => new()
    {
        { 0L, DurationUnit.Ticks },
        { 37_230_040_000L, DurationUnit.Ticks },
        { -36_000_000_000L, DurationUnit.Ticks },
        { TimeSpan.TicksPerDay + TimeSpan.TicksPerHour, DurationUnit.Ticks },
        { 0L, DurationUnit.Microseconds },
        { 3_723_004L, DurationUnit.Microseconds },
        { -3_600_000_000L, DurationUnit.Microseconds },
        { 90_000_000_000L, DurationUnit.Microseconds },
        { 0L, DurationUnit.Milliseconds },
        { 3_723_004L, DurationUnit.Milliseconds },
        { -3_600_000L, DurationUnit.Milliseconds },
        { 90_000_000L, DurationUnit.Milliseconds },
        { 0L, DurationUnit.Seconds },
        { 3_723L, DurationUnit.Seconds },
        { -3_600L, DurationUnit.Seconds },
        { 90_000L, DurationUnit.Seconds },
        { 0L, DurationUnit.Minutes },
        { 62L, DurationUnit.Minutes },
        { -60L, DurationUnit.Minutes },
        { 1_500L, DurationUnit.Minutes },
        { 0L, DurationUnit.Hours },
        { 1L, DurationUnit.Hours },
        { -1L, DurationUnit.Hours },
        { 25L, DurationUnit.Hours },
        { 0L, DurationUnit.Days },
        { 1L, DurationUnit.Days },
        { -1L, DurationUnit.Days },
        { 2L, DurationUnit.Days },
    };

    /// <summary>
    /// T259 R18 (pre-fix prerequisite): the by-name projection of a <c>time</c> column with a
    /// <see cref="TimeSpan"/> target must render the physical column — required and nullable alike.
    /// SQL generation only; the mapper is covered separately.
    /// </summary>
    [Fact]
    public void Column_TimeSpan_reference_renders_as_column()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ITimeEntity>().Select(t => new
        {
            Nullable = SqlFunctions.Column<TimeSpan?>(t, "col"),
            Required = SqlFunctions.Column<TimeSpan>(t, "col"),
        }));

        sql.Should().Be("select [col] as [Nullable], [col] as [Required]\n from t259_time_entity");
    }

    /// <summary>
    /// T259 R01 (the repository red): a driver-returned native <see cref="TimeSpan"/> materialises into
    /// a nullable <see cref="TimeSpan"/> target without passing through <c>Convert.ToInt64</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(NativeTimeCases))]
    public void Native_time_value_materialises_nullable_TimeSpan(TimeSpan value)
    {
        var body = Map(typeof(TimeSpan?));

        Invoke(body, value, typeof(TimeSpan)).Should().Be(value);
    }

    /// <summary>A required (non-nullable) target reads the same native value.</summary>
    [Theory]
    [MemberData(nameof(NativeTimeCases))]
    public void Native_time_value_materialises_required_TimeSpan(TimeSpan value)
    {
        var body = Map(typeof(TimeSpan));

        Invoke(body, value, typeof(TimeSpan)).Should().Be(value);
    }

    /// <summary>
    /// A declared storage unit scales integer storage only: a native <c>time</c> value is already a
    /// <see cref="TimeSpan"/> and must come back unscaled for every declared unit.
    /// </summary>
    [Theory]
    [MemberData(nameof(DurationUnits))]
    public void Native_time_value_ignores_DurationUnit(DurationUnit unit)
    {
        var body = Map(typeof(TimeSpan?), durationUnit: unit);

        Invoke(body, Fractional, typeof(TimeSpan)).Should().Be(Fractional);
    }

    [Fact]
    public void DbNull_short_circuits_nullable_TimeSpan()
    {
        var body = Map(typeof(TimeSpan?));

        Invoke(body, DBNull.Value, typeof(TimeSpan)).Should().BeNull();
    }

    [Fact]
    public void DbNull_short_circuits_DefaultOnNull()
    {
        var body = Map(typeof(TimeSpan), defaultOnNull: true);

        Invoke(body, DBNull.Value, typeof(TimeSpan)).Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// A required target has no null policy: SQL NULL must fail loudly instead of silently becoming
    /// <see cref="TimeSpan.Zero"/>.
    /// </summary>
    [Fact]
    public void DbNull_does_not_default_required_TimeSpan()
    {
        var body = Map(typeof(TimeSpan));

        var exception = InvokeFailing(body, DBNull.Value, typeof(TimeSpan));

        exception.Should().BeOfType<InvalidCastException>(
            "a required non-nullable TimeSpan must not silently fall back to default(TimeSpan)");
    }

    /// <summary>
    /// The unchanged integer-backed duration path: every unit keeps its scaling, and negative as well
    /// as 24 h-and-beyond values keep their range.
    /// </summary>
    [Theory]
    [MemberData(nameof(StoredDurationCases))]
    public void Stored_duration_preserves_unit_and_range(long stored, DurationUnit unit)
    {
        var body = Map(typeof(TimeSpan), durationUnit: unit);

        Invoke(body, stored, typeof(long)).Should().Be(TimeSpan.FromTicks(stored * TicksPerUnit(unit)));
    }

    /// <summary>
    /// A configured converter keeps first precedence over the native-time probe, and it is applied
    /// exactly once per read.
    /// </summary>
    [Fact]
    public void Configured_converter_precedes_native_time_mapping()
    {
        var converter = new OffsetTimeConverter(TimeSpan.FromHours(5));
        var body = Map(typeof(TimeSpan?), converter: converter);

        var value = Invoke(body, Fractional, typeof(TimeSpan));

        value.Should().Be(Fractional + converter.Offset);
        converter.ReadCount.Should().Be(1);
    }

    /// <summary>
    /// One compiled delegate handles a native-time reader and then an integer reader: the driver type
    /// is discriminated per invocation, never captured at construction time.
    /// </summary>
    [Fact]
    public void Cached_mapper_discriminates_each_reader()
    {
        var lambda = Expression.Lambda(Map(typeof(TimeSpan?)), Record).Compile();

        lambda.DynamicInvoke(new StrictStorageReader(Fractional, typeof(TimeSpan))).Should().Be(Fractional);
        lambda.DynamicInvoke(new StrictStorageReader(37_230_040_000L, typeof(long))).Should().Be(Fractional);
    }

    /// <summary>Non-duration targets are untouched by the native-time branch.</summary>
    [Fact]
    public void Non_duration_mapping_retains_existing_path()
    {
        Invoke(Map(typeof(int)), 42, typeof(int)).Should().Be(42);
        Invoke(Map(typeof(int?)), 42, typeof(int)).Should().Be(42);
        Invoke(Map(typeof(int?)), DBNull.Value, typeof(int)).Should().BeNull();

        var moment = new DateTime(2026, 10, 25, 1, 2, 3, DateTimeKind.Unspecified);
        Invoke(Map(typeof(DateTime)), moment, typeof(DateTime)).Should().Be(moment);

        Invoke(Map(typeof(string)), "abc", typeof(string)).Should().Be("abc");
    }

    private static long TicksPerUnit(DurationUnit unit) => unit switch
    {
        DurationUnit.Ticks => 1L,
        DurationUnit.Microseconds => TimeSpan.TicksPerMicrosecond,
        DurationUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
        DurationUnit.Seconds => TimeSpan.TicksPerSecond,
        DurationUnit.Minutes => TimeSpan.TicksPerMinute,
        DurationUnit.Hours => TimeSpan.TicksPerHour,
        DurationUnit.Days => TimeSpan.TicksPerDay,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown duration unit."),
    };

    private static Expression Map(
        Type dest,
        bool defaultOnNull = false,
        DurationUnit? durationUnit = null,
        IPropertyValueConverter? converter = null)
    {
        var column = new SelectExpression(dest)
        {
            Index = 0,
            PropertyName = "V",
            DurationUnit = durationUnit,
            Converter = converter,
        };

        if (defaultOnNull)
        {
            // DefaultOnNull is set internally by the *OrDefault scalar terminals; the property setter is
            // internal to nextorm.core, so drive it from the test via reflection.
            typeof(SelectExpression).GetProperty(nameof(SelectExpression.DefaultOnNull))!.SetValue(column, true);
        }

        return SqlServerTestContext.CreateSqlServer().MapColumnExpression(column, Record);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static object? Invoke(Expression body, object? value, Type storage)
        => Expression.Lambda(body, Record).Compile().DynamicInvoke(new StrictStorageReader(value, storage));

    private static Exception InvokeFailing(Expression body, object? value, Type storage)
    {
        var lambda = Expression.Lambda(body, Record).Compile();
        try
        {
            lambda.DynamicInvoke(new StrictStorageReader(value, storage));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }

        throw new InvalidOperationException("The mapper was expected to fail but returned a value.");
    }

    /// <summary>
    /// Adds a fixed offset to every provider value so the test can tell the converter's result from the
    /// raw column value, and counts the reads so a double conversion is visible.
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
    /// A one-column reader with a declared storage type. Every typed getter throws when it does not match
    /// the storage type, so a native-time value can only be read through the boxed
    /// <see cref="GetValue"/> — exactly as SqlClient exposes <c>time</c>, where
    /// <see cref="TimeSpan"/> is a legal field type and a typed getter for any other CLR type must fail.
    /// </summary>
    private sealed class StrictStorageReader(object? value, Type storage) : DbDataReader
    {
        public override int FieldCount => 1;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => -1;

        public override int Depth => 0;

        public override int VisibleFieldCount => 1;

        public override object this[int ordinal] => throw new NotSupportedException();

        public override object this[string name] => throw new NotSupportedException();

        public override object GetValue(int ordinal) => ordinal == 0 ? value ?? DBNull.Value : throw new IndexOutOfRangeException();

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override Type GetFieldType(int ordinal) => storage;

        public override string GetDataTypeName(int ordinal) => storage.Name;

        public override string GetName(int ordinal) => "V";

        public override int GetOrdinal(string name) => 0;

        public override bool Read() => throw new NotSupportedException();

        public override bool NextResult() => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => throw new NotSupportedException();

        public override int GetValues(object[] values) => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => Require<bool>(ordinal);

        public override byte GetByte(int ordinal) => Require<byte>(ordinal);

        public override short GetInt16(int ordinal) => Require<short>(ordinal);

        public override int GetInt32(int ordinal) => Require<int>(ordinal);

        public override long GetInt64(int ordinal) => Require<long>(ordinal);

        public override float GetFloat(int ordinal) => Require<float>(ordinal);

        public override double GetDouble(int ordinal) => Require<double>(ordinal);

        public override decimal GetDecimal(int ordinal) => Require<decimal>(ordinal);

        public override string GetString(int ordinal) => Require<string>(ordinal);

        public override Guid GetGuid(int ordinal) => Require<Guid>(ordinal);

        public override DateTime GetDateTime(int ordinal) => Require<DateTime>(ordinal);

        public override char GetChar(int ordinal) => Require<char>(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        private T Require<T>(int ordinal)
        {
            if (ordinal != 0)
                throw new IndexOutOfRangeException();

            if (storage != typeof(T))
                throw new InvalidCastException($"Get{typeof(T).Name} called for storage {storage.Name}.");

            return (T)value!;
        }
    }
}
