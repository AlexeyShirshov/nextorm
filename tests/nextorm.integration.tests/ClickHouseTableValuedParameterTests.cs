using ClickHouse.Driver;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse emulates a table-valued parameter with a native array bound through the public
/// <see cref="ProcedureParameter.Table{T}(string, IEnumerable{T})"/> factory and expanded server-side
/// with <c>arrayJoin(@p)</c>: a scalar set binds as an <c>Array(T)</c> and an entity set as
/// <c>Array(Tuple(...))</c>. Unlike the in-memory unit tests in <c>nextorm.clickhouse.tests</c>, these
/// run against the real server, so the driver binding and the server expansion are exercised end to end.
/// </summary>
public sealed class ClickHouseTableValuedParameterTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private sealed class TvpRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class TemporalRow
    {
        public int Id { get; set; }
        public TimeOnly At { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    private sealed class NullableTemporalRow
    {
        public int Id { get; set; }
        public TimeOnly? At { get; set; }
        public TimeSpan? Elapsed { get; set; }
    }

    // The server returns a ClickHouse String for the TimeOnly column and an Int64 for the duration, so
    // the live projection reads them in that shape (the CLR mapping on the write side is what is tested).
    private sealed class TemporalProjection
    {
        public int Id { get; set; }
        public string At { get; set; } = string.Empty;
        public long Elapsed { get; set; }
    }

    private sealed class NullableTemporalProjection
    {
        public int Id { get; set; }
        public string? At { get; set; }
        public long? Elapsed { get; set; }
    }

    private enum Level : int
    {
        Low = 1,
        High = 2,
    }

    private sealed class EnumRow
    {
        public int Id { get; set; }
        public Level Kind { get; set; }
    }

    private sealed class EnumProjection
    {
        public int Id { get; set; }
        public int Kind { get; set; }
    }

    private sealed class BytesRow
    {
        public int Id { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    // Core widens sbyte/ushort/uint/ulong for PostgreSQL; ClickHouse binds the native CLR types, so a
    // scalar set and the matching entity column must resolve to the same ClickHouse type.
    private sealed class NativeScalarRow
    {
        public sbyte I8 { get; set; }
        public ushort U16 { get; set; }
        public uint U32 { get; set; }
        public ulong U64 { get; set; }
        public char Ch { get; set; }
    }

    private sealed class NativeTypeProjection
    {
        public string ScalarType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
    }

    private sealed class DecimalRow
    {
        public int Id { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal Amount { get; set; }

        public decimal Plain { get; set; }
    }

    private sealed class DecimalTypeProjection
    {
        public string AmountType { get; set; } = string.Empty;
        public string AmountValue { get; set; } = string.Empty;
        public string PlainType { get; set; } = string.Empty;
    }

    private sealed class NullableDecimalRow
    {
        public int Id { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal? Amount { get; set; }
    }

    private sealed class NullableDecimalProjection
    {
        public int Id { get; set; }
        public string AmountType { get; set; } = string.Empty;
        public bool IsNull { get; set; }
        public string AmountValue { get; set; } = string.Empty;
    }

    private sealed class IntSecondsToDurationConverter : ValueConverter<int, TimeSpan>
    {
        public override TimeSpan ConvertToProvider(int model) => TimeSpan.FromSeconds(model);

        public override int ConvertFromProvider(TimeSpan provider) => (int)provider.TotalSeconds;
    }

    private sealed class ConvertedDurationRow
    {
        public int Id { get; set; }

        [ValueConverter(typeof(IntSecondsToDurationConverter))]
        public int Elapsed { get; set; }
    }

    // ClickHouse binds a byte[] column as String; the driver hands it back as a CLR string, so the
    // read side projects it as string (a byte[] projection is not supported by the row reader).
    private sealed class BytesProjection
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    [Fact]
    public void ExecuteRaw_TableParameter_Scalar_ShouldExpandWithArrayJoin()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@ids) as value order by value",
            [ProcedureParameter.Table("ids", new[] { 3, 1, 2 })]);

        result.Read<int>().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExecuteRawAsync_TableParameter_Scalar_ShouldExpandWithArrayJoin()
    {
        var ctx = _sut.DataProvider;

        await using var result = await ctx.ExecuteRawAsync(
            "select arrayJoin(@ids) as value order by value",
            [ProcedureParameter.Table("ids", new[] { 5, 4 })],
            TestContext.Current.CancellationToken);

        var read = new List<int>();
        await foreach (var value in result.ReadAsync<int>(TestContext.Current.CancellationToken))
            read.Add(value);

        read.Should().Equal(4, 5);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_Entity_ShouldExpandTupleRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as Name from (select arrayJoin(@rows) as t) order by Id",
            [ProcedureParameter.Table("rows", new[]
            {
                new TvpRow { Id = 2, Name = "beta" },
                new TvpRow { Id = 1, Name = null },
            })]);

        var read = result.Read<TvpRow>();

        read.Should().HaveCount(2);
        read[0].Id.Should().Be(1);
        read[0].Name.Should().BeNull();
        read[1].Id.Should().Be(2);
        read[1].Name.Should().Be("beta");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyScalar_ShouldReturnNoRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@ids) as value",
            [ProcedureParameter.Table("ids", Array.Empty<int>())]);

        result.Read<int>().Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyEntity_ShouldReturnNoRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as Name from (select arrayJoin(@rows) as t)",
            [ProcedureParameter.Table("rows", Array.Empty<TvpRow>())]);

        result.Read<TvpRow>().Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_TableParameterWithTypeName_ShouldThrowArgumentException()
    {
        var ctx = _sut.DataProvider;

        var act = () => ctx.ExecuteRaw("select 1", [ProcedureParameter.Table("p", "dbo.TvpRow", new[] { 1 })]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_ScalarTimeOnly_ShouldBindInvariantStrings()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@times) as value order by value",
            [ProcedureParameter.Table("times", new[] { new TimeOnly(3, 4, 5, 123), new TimeOnly(1, 2, 3) })]);

        result.Read<string>().Should().Equal("01:02:03.0000000", "03:04:05.1230000");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_ScalarTimeSpan_ShouldBindTicks()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@durations) as value order by value",
            [ProcedureParameter.Table("durations", new[] { TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1) })]);

        result.Read<long>().Should().Equal(10_000_000L, 600_000_000L);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EntityTemporal_ShouldMatchScalarRepresentation()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as At, t.3 as Elapsed from (select arrayJoin(@rows) as t) order by Id",
            [ProcedureParameter.Table("rows", new[]
            {
                new TemporalRow { Id = 1, At = new TimeOnly(3, 4, 5, 123), Elapsed = TimeSpan.FromMinutes(1) },
            })]);

        var read = result.Read<TemporalProjection>();

        read.Should().ContainSingle();
        read[0].Id.Should().Be(1);
        read[0].At.Should().Be("03:04:05.1230000");
        read[0].Elapsed.Should().Be(600_000_000L);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_NullableTemporalEntity_ShouldKeepNulls()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as At, t.3 as Elapsed from (select arrayJoin(@rows) as t) order by Id",
            [ProcedureParameter.Table("rows", new[]
            {
                new NullableTemporalRow { Id = 1, At = null, Elapsed = null },
                new NullableTemporalRow { Id = 2, At = new TimeOnly(1, 2, 3), Elapsed = TimeSpan.FromSeconds(1) },
            })]);

        var read = result.Read<NullableTemporalProjection>();

        read.Should().HaveCount(2);
        read[0].At.Should().BeNull();
        read[0].Elapsed.Should().BeNull();
        read[1].At.Should().Be("01:02:03.0000000");
        read[1].Elapsed.Should().Be(10_000_000L);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyTemporalEntity_ShouldReturnNoRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as At, t.3 as Elapsed from (select arrayJoin(@rows) as t)",
            [ProcedureParameter.Table("rows", Array.Empty<NullableTemporalRow>())]);

        result.Read<NullableTemporalProjection>().Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_ScalarEnum_ShouldBindUnderlyingNumber()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@levels) as value order by value",
            [ProcedureParameter.Table("levels", new[] { Level.High, Level.Low })]);

        result.Read<int>().Should().Equal(1, 2);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EntityEnum_ShouldBindUnderlyingNumber()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as Kind from (select arrayJoin(@rows) as t) order by Id",
            [ProcedureParameter.Table("rows", new[] { new EnumRow { Id = 1, Kind = Level.High } })]);

        var read = result.Read<EnumProjection>();

        read.Should().ContainSingle();
        read[0].Kind.Should().Be(2);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_ScalarBytes_ShouldBindStringElement()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@blobs) as value order by value",
            [ProcedureParameter.Table("blobs", new[] { new byte[] { 65, 66, 67 } })]);

        result.Read<string>().Should().Equal("ABC");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EntityBytes_ShouldBindStringElement()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, t.2 as Data from (select arrayJoin(@rows) as t)",
            [ProcedureParameter.Table("rows", new[] { new BytesRow { Id = 1, Data = [65, 66, 67] } })]);

        var read = result.Read<BytesProjection>();

        read.Should().ContainSingle();
        read[0].Data.Should().Be("ABC");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalColumn_ShouldUseDeclaredAndDefaultPrecision()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select toTypeName(t.2) as AmountType, toString(t.2) as AmountValue, toTypeName(t.3) as PlainType "
            + "from (select arrayJoin(@rows) as t) limit 1",
            [ProcedureParameter.Table("rows", new[] { new DecimalRow { Id = 1, Amount = 12.3456m, Plain = 1.5m } })]);

        var row = result.Read<DecimalTypeProjection>().Single();

        // [DecimalPrecision(12, 4)] binds as Decimal(12, 4); an unconfigured decimal column keeps
        // ClickHouse's provider default Decimal(38, 10).
        row.AmountType.Should().Be("Decimal(12, 4)");
        row.AmountValue.Should().Be("12.3456");
        row.PlainType.Should().Be("Decimal(38, 10)");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalNegativeValue_ShouldUseDeclaredPrecision()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select toTypeName(t.2) as AmountType, toString(t.2) as AmountValue "
            + "from (select arrayJoin(@rows) as t) limit 1",
            [ProcedureParameter.Table("rows", new[] { new DecimalRow { Id = 1, Amount = -12.3456m, Plain = 1.5m } })]);

        var row = result.Read<DecimalTypeProjection>().Single();

        // A declared Decimal(12, 4) keeps the sign; the negative value is not widened to the default.
        row.AmountType.Should().Be("Decimal(12, 4)");
        row.AmountValue.Should().Be("-12.3456");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_NullableDecimalColumn_ShouldBeNullableAndKeepNull()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select t.1 as Id, toTypeName(t.2) as AmountType, isNull(t.2) as IsNull, toString(t.2) as AmountValue "
            + "from (select arrayJoin(@rows) as t) order by Id",
            [ProcedureParameter.Table("rows", new[]
            {
                new NullableDecimalRow { Id = 1, Amount = -12.3456m },
                new NullableDecimalRow { Id = 2, Amount = null },
            })]);

        var read = result.Read<NullableDecimalProjection>();

        // A nullable decimal honors the declared precision/scale inside Nullable(...), and a null row
        // keeps NULL instead of a zero default. The negative value round-trips through the driver.
        read.Should().HaveCount(2);
        read[0].AmountType.Should().Be("Nullable(Decimal(12, 4))");
        read[0].IsNull.Should().BeFalse();
        read[0].AmountValue.Should().Be("-12.3456");
        read[1].AmountType.Should().Be("Nullable(Decimal(12, 4))");
        read[1].IsNull.Should().BeTrue();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalOverflow_ShouldThrowServerException()
    {
        var ctx = _sut.DataProvider;

        // 123456789.1234 needs 13 digits but Decimal(12, 4) holds 12; the server rejects the tuple
        // parameter (ARGUMENT_OUT_OF_BOUND) instead of silently corrupting the value.
        var act = () =>
        {
            using var result = ctx.ExecuteRaw(
                "select toTypeName(t.2) as AmountType, toString(t.2) as AmountValue, toTypeName(t.3) as PlainType "
                + "from (select arrayJoin(@rows) as t) limit 1",
                [ProcedureParameter.Table("rows", new[] { new DecimalRow { Id = 1, Amount = 123456789.1234m, Plain = 1.5m } })]);
        };

        act.Should().Throw<ClickHouseServerException>().WithMessage("*Decimal value is too big*");
    }

    [Fact]
    public void ExecuteRaw_TableParameter_NativeScalarTypes_ShouldMatchEntityColumnTypes()
    {
        AssertScalarTypeMatchesEntityTuple(new sbyte[] { -1 }, entityIndex: 1, expected: "Int8");
        AssertScalarTypeMatchesEntityTuple(new ushort[] { 2 }, entityIndex: 2, expected: "UInt16");
        AssertScalarTypeMatchesEntityTuple(new uint[] { 3 }, entityIndex: 3, expected: "UInt32");
        AssertScalarTypeMatchesEntityTuple(new ulong[] { 4 }, entityIndex: 4, expected: "UInt64");
        AssertScalarTypeMatchesEntityTuple(new[] { 'x' }, entityIndex: 5, expected: "String");
    }

    private void AssertScalarTypeMatchesEntityTuple<T>(T[] scalar, int entityIndex, string expected)
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            $"select toTypeName(@scalar) as ScalarType, toTypeName(t.{entityIndex}) as EntityType "
            + "from (select arrayJoin(@rows) as t) limit 1",
            [
                ProcedureParameter.Table("scalar", scalar),
                ProcedureParameter.Table("rows", new[] { new NativeScalarRow { I8 = -1, U16 = 2, U32 = 3, U64 = 4, Ch = 'x' } }),
            ]);

        var row = result.Read<NativeTypeProjection>().Single();
        row.ScalarType.Should().Be($"Array({expected})");
        row.EntityType.Should().Be(expected);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_NativeScalarTypes_ShouldPreserveValues()
    {
        ReadJoined(new sbyte[] { -1, 127 }).Should().Be("-1,127");
        ReadJoined(new ushort[] { 2, 65535 }).Should().Be("2,65535");
        ReadJoined(new uint[] { 3, uint.MaxValue }).Should().Be("3,4294967295");
        ReadJoined(new ulong[] { 4, ulong.MaxValue }).Should().Be("4,18446744073709551615");
        ReadJoined(new[] { 'a', 'b' }).Should().Be("a,b");
    }

    private string ReadJoined<T>(T[] values)
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select toString(v) as value from (select arrayJoin(@v) as v) order by v",
            [ProcedureParameter.Table("v", values)]);

        return string.Join(",", result.Read<string>());
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyNativeScalar_ShouldReturnNoRows()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select arrayJoin(@v) as value",
            [ProcedureParameter.Table("v", Array.Empty<sbyte>())]);

        result.Read<sbyte>().Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EntityConverterToTimeSpan_OnNonNativeProvider_ShouldThrow()
    {
        var ctx = _sut.DataProvider;

        // ClickHouse has no native duration type, so a value converter whose provider type is TimeSpan
        // has no bindable column representation: the parameter is rejected when the columns are built,
        // before any row is consumed.
        var act = () => ctx.ExecuteRaw(
            "select t.1 as Id, toString(t.2) as Elapsed from (select arrayJoin(@rows) as t)",
            [ProcedureParameter.Table("rows", new[] { new ConvertedDurationRow { Id = 1, Elapsed = 90 } })]);

        act.Should().Throw<NotSupportedException>().WithMessage("*Elapsed*");
    }
}
