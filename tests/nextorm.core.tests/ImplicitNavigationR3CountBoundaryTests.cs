using System.Collections;
using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B r3 A3′ (D-R3-3): the checked materialization boundary. A navigation <c>Count()</c> /
/// property <c>Count</c> column is read from the wide (bigint) scalar with <c>GetInt64</c> and narrowed
/// with a checked <c>long-&gt;int</c> conversion, so an out-of-range count throws a uniform
/// <see cref="OverflowException"/> instead of relying on the provider's <c>GetInt32</c> widening. An
/// ordinary Int32 projection keeps <c>GetInt32</c> (does not switch to bigint) and a plain Int64
/// projection is unchanged.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR3CountBoundaryTests
{
    // The navigation registration uses the process-wide DataContextCache; other classes in this
    // collection clear it, so this class must be serialized with them and start from a clean cache.
    public ImplicitNavigationR3CountBoundaryTests() => DataContextCache.Clear();

    private const long Wide = (long)int.MaxValue + 1;

    private static Func<IDataRecord, T> CompileMapper<T>(SelectExpression column)
    {
        var param = Expression.Parameter(typeof(IDataRecord), "r");
        var body = RowMapperFactory.MapColumn(column, param);
        return Expression.Lambda<Func<IDataRecord, T>>(body, param).Compile();
    }

    private static SelectExpression WideCountColumn()
        => new(typeof(int)) { Index = 0, PropertyName = "C", IsWideCountNarrowed = true };

    [Fact]
    public void Wide_count_int_materialization_passes_an_in_range_value()
    {
        var mapper = CompileMapper<int>(WideCountColumn());

        mapper(new LongRecord(42)).Should().Be(42);
        mapper(new LongRecord(int.MaxValue)).Should().Be(int.MaxValue);
    }

    [Fact]
    public void Wide_count_int_materialization_throws_overflow_when_out_of_range()
    {
        var mapper = CompileMapper<int>(WideCountColumn());

        Action act = () => mapper(new LongRecord(Wide));

        act.Should().Throw<OverflowException>(
            "an out-of-range wide navigation count read into an Int32 must narrow with a checked conversion");
    }

    [Fact]
    public void Wide_count_int_materialization_reads_int64_not_int32()
    {
        var param = Expression.Parameter(typeof(IDataRecord), "r");

        var body = RowMapperFactory.MapColumn(WideCountColumn(), param).ToString();

        body.Should().Contain(nameof(IDataRecord.GetInt64));
        body.Should().NotContain(nameof(IDataRecord.GetInt32));
    }

    [Fact]
    public void Ordinary_int_projection_still_reads_int32_and_does_not_touch_bigint()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" };
        var param = Expression.Parameter(typeof(IDataRecord), "r");

        var body = RowMapperFactory.MapColumn(column, param).ToString();
        body.Should().Contain(nameof(IDataRecord.GetInt32)).And.NotContain(nameof(IDataRecord.GetInt64));

        // Behavioral control: the ordinary int reader never calls GetInt64 (the record throws there).
        var mapper = CompileMapper<int>(column);
        mapper(new IntOnlyRecord(7)).Should().Be(7);
    }

    [Fact]
    public void Plain_long_projection_keeps_the_full_64_bit_value()
    {
        var column = new SelectExpression(typeof(long)) { Index = 0, PropertyName = "L" };
        var mapper = CompileMapper<long>(column);

        mapper(new LongRecord(Wide)).Should().Be(Wide);
    }

    [Fact]
    public void Wide_count_flag_participates_in_the_plan_identity()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<R24Parent>().Select(p => new { p.Id, C = p.Children.Count() });
        var comparer = command.GetSelectExpressionPlanEqualityComparer();

        var narrowed = new SelectExpression(typeof(int)) { Index = 1, PropertyName = "C", IsWideCountNarrowed = true };
        var ordinary = new SelectExpression(typeof(int)) { Index = 1, PropertyName = "C" };

        comparer.Equals(narrowed, ordinary).Should().BeFalse(
            "a wide-count int column must never share a cached plan/mapper with an ordinary int column");
        comparer.GetHashCode(narrowed).Should().NotBe(comparer.GetHashCode(ordinary));
    }

    [Fact]
    public void Prepared_wide_count_int_column_is_tagged_and_survives_the_cache_clone()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<R24Child>();
        ctx.From<R24Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        var command = ctx.From<R24Parent>().Select(p => new { p.Id, C = p.Children.Count() });
        ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        var column = command.SelectList!.Single(c => c.PropertyName == "C");
        column.IsWideCountNarrowed.Should().BeTrue("the preparer must tag the materialized Count column (A3′)");

        var clone = command.CloneForCache();
        clone.SelectList!.Single(c => c.PropertyName == "C").IsWideCountNarrowed.Should().BeTrue(
            "the provenance flag must survive the plan-cache clone");
    }

    /// <summary>A reader whose only typed getter is <see cref="IDataRecord.GetInt64"/>.</summary>
    private sealed class LongRecord(long value) : IDataRecord
    {
        public int FieldCount => 1;
        public object this[int i] => value;
        public object this[string name] => value;
        public bool IsDBNull(int i) => false;
        public object GetValue(int i) => value;
        public long GetInt64(int i) => value;
        public int GetInt32(int i) => throw new InvalidOperationException("GetInt32 must not be called for a wide count column");
        public string GetName(int i) => "c";
        public int GetOrdinal(string name) => 0;
        public int GetValues(object[] values) { values[0] = value; return 1; }
        public string GetDataTypeName(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => throw new NotSupportedException();
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public string GetString(int i) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public IEnumerator GetEnumerator() => throw new NotSupportedException();
    }

    /// <summary>A reader whose only typed getter is <see cref="IDataRecord.GetInt32"/>.</summary>
    private sealed class IntOnlyRecord(int value) : IDataRecord
    {
        public int FieldCount => 1;
        public object this[int i] => value;
        public object this[string name] => value;
        public bool IsDBNull(int i) => false;
        public object GetValue(int i) => value;
        public int GetInt32(int i) => value;
        public long GetInt64(int i) => throw new InvalidOperationException("GetInt64 must not be called for an ordinary int column");
        public string GetName(int i) => "id";
        public int GetOrdinal(string name) => 0;
        public int GetValues(object[] values) { values[0] = value; return 1; }
        public string GetDataTypeName(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => throw new NotSupportedException();
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public string GetString(int i) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public IEnumerator GetEnumerator() => throw new NotSupportedException();
    }
}
