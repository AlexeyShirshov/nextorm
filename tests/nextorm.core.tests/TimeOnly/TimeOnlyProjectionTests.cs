using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T340 (#340) DB-free contract for the <see cref="TimeOnly"/> projection: the value domain of the shared
/// reader helper and the composition rules of the row mapper that emits it.
/// <para>
/// The mapper owns the provider policy (a refused provider must throw while the mapper is composed, before
/// the nullable/default-on-null wrappers), while the helper owns the value domain (a native
/// <see cref="TimeOnly"/> or an in-day <see cref="TimeSpan"/>, and nothing else). Both are asserted here so
/// a driver- or provider-specific quirk cannot widen either rule.
/// </para>
/// </summary>
public class TimeOnlyProjectionTests
{
    private const string Refusal = "TimeOnly projection refused for this provider: no verified native time-of-day reader.";

    private static string OutOfDomain => TimeOnlyProjectionReader.OutOfDomainMessage;

    private static string Alien => TimeOnlyProjectionReader.AlienValueMessage;

    /// <summary>A native <see cref="TimeOnly"/> is returned unchanged (no re-derivation, no rounding).</summary>
    [Fact]
    public void ReadNative_TimeOnly_ShouldPassThrough()
    {
        var value = new TimeOnly(13, 45, 7, 123).Add(TimeSpan.FromTicks(4_567));

        TimeOnlyProjectionReader.ReadNative(value, refusal: null).Should().Be(value);
        TimeOnlyProjectionReader.ReadNative(value, refusal: null).Ticks.Should().Be(495_071_234_567);
    }

    /// <summary>An in-day <see cref="TimeSpan"/> (the driver shape of a native <c>time</c> column) converts exactly.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(100L)]
    [InlineData(495_071_234_567L)]
    [InlineData(TimeSpan.TicksPerDay - 1)]
    public void ReadNative_InDayTimeSpan_ShouldConvertExactly(long ticks)
    {
        var value = TimeSpan.FromTicks(ticks);

        TimeOnlyProjectionReader.ReadNative(value, refusal: null).Ticks.Should().Be(ticks);
    }

    /// <summary>A duration is not a time of day: negative and 24 h values are refused, never wrapped.</summary>
    [Theory]
    [InlineData(-1L)]
    [InlineData(-TimeSpan.TicksPerHour)]
    [InlineData(TimeSpan.TicksPerDay)]
    [InlineData(TimeSpan.TicksPerDay + 1)]
    [InlineData(25L * TimeSpan.TicksPerHour)]
    public void ReadNative_OutOfDomainTimeSpan_ShouldRefuse(long ticks)
    {
        Action act = () => TimeOnlyProjectionReader.ReadNative(TimeSpan.FromTicks(ticks), refusal: null);

        act.Should().Throw<NotSupportedException>().WithMessage(OutOfDomain);
    }

    /// <summary>Only native time representations are accepted: no string parsing, no integer reinterpretation.</summary>
    [Fact]
    public void ReadNative_AlienRepresentation_ShouldRefuse()
    {
        foreach (var value in new object?[] { 495_071_234_567L, 49_507_123, "13:45:07.1234567", 13.45m, DBNull.Value, null })
        {
            var captured = value;
            Action act = () => TimeOnlyProjectionReader.ReadNative(captured, refusal: null);

            act.Should().Throw<NotSupportedException>().WithMessage(Alien, $"'{captured ?? "<null>"}' is not a native time value");
        }
    }

    /// <summary>The provider policy wins over the value: a refused provider cannot be satisfied by a driver value.</summary>
    [Fact]
    public void ReadNative_PolicyRefusal_ShouldWinOverANativeValue()
    {
        Action act = () => TimeOnlyProjectionReader.ReadNative(new TimeOnly(1, 2, 3), Refusal);

        act.Should().Throw<NotSupportedException>().WithMessage(Refusal);
    }

    /// <summary>
    /// A refused provider throws while the mapper is composed, before the nullable wrapper could turn the
    /// refusal into a "supported, but null" projection.
    /// </summary>
    [Fact]
    public void MapColumn_RefusedProvider_ShouldThrowDuringComposition()
    {
        var column = NullableColumn();
        var record = Expression.Parameter(typeof(IDataRecord), "record");

        Action act = () => RowMapperFactory.MapColumn(column, record, supportsNativeDuration: true, dialect: null, timeOnlyRefusal: Refusal);

        act.Should().Throw<NotSupportedException>().WithMessage(Refusal);
    }

    /// <summary>A permitted provider composes a mapper that reads the value through the shared helper.</summary>
    [Fact]
    public void MapColumn_PermittedProvider_ShouldReadNativeValues()
    {
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var body = RowMapperFactory.MapColumn(NullableColumn(), record, supportsNativeDuration: true, dialect: null, timeOnlyRefusal: null);
        var mapper = Expression.Lambda<Func<IDataRecord, TimeOnly?>>(body, record).Compile();

        mapper(new ScalarRecord(TimeSpan.FromTicks(495_071_234_560))).Should().Be(new TimeOnly(13, 45, 7, 123).Add(TimeSpan.FromTicks(4_560)));
        mapper(new ScalarRecord(TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1)))!.Value.Ticks.Should().Be(TimeSpan.TicksPerDay - 1);
        mapper(new ScalarRecord(new TimeOnly(23, 59, 59)))!.Value.Ticks.Should().Be(863_990_000_000);
        mapper(new ScalarRecord(DBNull.Value)).Should().BeNull("SQL NULL short-circuits before the native read");

        Action outOfDomain = () => mapper(new ScalarRecord(TimeSpan.FromTicks(TimeSpan.TicksPerDay)));
        outOfDomain.Should().Throw<NotSupportedException>().WithMessage(OutOfDomain);

        Action alien = () => mapper(new ScalarRecord(42));
        alien.Should().Throw<NotSupportedException>().WithMessage(Alien);
    }

    // The constructor derives Nullable from the declared type, so this column exercises the nullable wrapper.
    private static SelectExpression NullableColumn() => new(typeof(TimeOnly?))
    {
        Index = 0,
        PropertyName = "T",
    };

    /// <summary>One column whose <see cref="IDataRecord.GetValue"/> returns the supplied boxed value.</summary>
    private sealed class ScalarRecord(object? value) : IDataRecord
    {
        public int FieldCount => 1;
        public object this[int i] => value!;
        public object this[string name] => value!;

        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "time";
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => value?.GetType() ?? typeof(object);
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public int GetInt32(int i) => throw new NotSupportedException();
        public long GetInt64(int i) => throw new NotSupportedException();
        public string GetName(int i) => "T";
        public int GetOrdinal(string name) => 0;
        public string GetString(int i) => throw new NotSupportedException();
        public object GetValue(int i) => value!;
        public int GetValues(object[] values) { values[0] = value!; return 1; }
        public bool IsDBNull(int i) => value is null or DBNull;
    }
}
