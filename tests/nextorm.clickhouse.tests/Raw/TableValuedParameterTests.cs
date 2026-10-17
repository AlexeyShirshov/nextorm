using System.Data;
using System.Data.Common;
using ClickHouse.Driver.ADO.Parameters;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse emulates a table-valued parameter with an array bound through the public
/// <see cref="ProcedureParameter.Table{T}(string, IEnumerable{T})"/> factory: a scalar set binds as a
/// typed <c>Array(T)</c> and an entity set as <c>Array(Tuple(...))</c> consumed with <c>arrayJoin(@p)</c>.
/// The provider sets the driver's explicit <c>ClickHouseType</c> (with <c>Nullable(...)</c> for a column
/// that can hold a null) because the driver infers a non-nullable type from a CLR value and otherwise
/// fails to serialize a null tuple element.
/// </summary>
[Trait("D162", "Conformance")]
public class TableValuedParameterTests
{
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

    private sealed class BlobRow
    {
        public int Id { get; set; }
        public byte[]? Payload { get; set; }
    }

    private sealed class NestedRow
    {
        public int Id { get; set; }
        public TvpRow Child { get; set; } = new();
    }

    private sealed class NativeScalarRow
    {
        public sbyte I8 { get; set; }
        public ushort U16 { get; set; }
        public uint U32 { get; set; }
        public ulong U64 { get; set; }
        public char Ch { get; set; }
    }

    private sealed class DecimalRow
    {
        public int Id { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal Amount { get; set; }

        public decimal Plain { get; set; }
    }

    private sealed class NullableDecimalRow
    {
        public int Id { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal? Amount { get; set; }
    }

    private sealed class ExposedContext : ClickHouseDataContext
    {
        public ExposedContext()
            : base(
                "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm",
                new DataContextBuilder())
        {
        }

        public DbParameter Create(ProcedureParameter parameter) => CreateProcedureParameter(parameter);
    }

    [Fact]
    public void SupportsTableValuedParameters_IsTrue()
    {
        ClickHouseTestContext.CreateClickHouse().Dialect.SupportsTableValuedParameters.Should().BeTrue();
    }

    [Fact]
    public void ScalarTableParameter_ShouldBindTypedArray()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("ids", new[] { 1, 2, 3 }));

        parameter.ClickHouseType.Should().Be("Array(Int32)");
        parameter.Value.Should().BeOfType<int[]>().Which.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void NullableScalarTableParameterWithNull_ShouldBindNullableArray()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("ids", new int?[] { 1, null, 3 }));

        parameter.ClickHouseType.Should().Be("Array(Nullable(Int32))");
        parameter.Value.Should().BeOfType<int?[]>().Which.Should().Equal(1, null, 3);
    }

    [Fact]
    public void ScalarTableParameterWithNullString_ShouldBindNullableArray()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("names", new string?[] { "a", null }));

        parameter.ClickHouseType.Should().Be("Array(Nullable(String))");
        parameter.Value.Should().BeOfType<string?[]>().Which.Should().Equal("a", null);
    }

    [Fact]
    public void EntityTableParameter_ShouldBindTupleArrayWithColumnTypes()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[] { new TvpRow { Id = 1, Name = "alpha" }, new TvpRow { Id = 2, Name = null } }));

        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Nullable(String)))");

        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows.Should().HaveCount(2);
        rows[0].Should().Equal(1, "alpha");
        rows[1].Should().Equal(2, null);
    }

    [Fact]
    public void EntityTableParameter_DecimalColumn_ShouldUseDeclaredAndDefaultPrecision()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[] { new DecimalRow { Id = 1, Amount = 12.3456m, Plain = 1.5m } }));

        // A [DecimalPrecision(12, 4)] column binds as Decimal(12, 4); an unconfigured decimal column
        // keeps ClickHouse's provider default Decimal(38, 10).
        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Decimal(12, 4), Decimal(38, 10)))");

        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows[0][1].Should().Be(12.3456m);
        rows[0][2].Should().Be(1.5m);
    }

    [Fact]
    public void EntityTableParameter_NullableDecimalColumn_ShouldUseNullableDeclaredPrecision()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[]
            {
                new NullableDecimalRow { Id = 1, Amount = 12.3456m },
                new NullableDecimalRow { Id = 2, Amount = null },
            }));

        // A nullable decimal honors the declared precision/scale inside Nullable(...), and a null row
        // stays null rather than defaulting to zero.
        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Nullable(Decimal(12, 4))))");

        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows.Should().HaveCount(2);
        rows[0][1].Should().Be(12.3456m);
        rows[1][1].Should().BeNull();
    }

    [Fact]
    public void EmptyScalarTableParameter_ShouldBindEmptyArray()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("ids", Array.Empty<int>()));

        parameter.ClickHouseType.Should().Be("Array(Int32)");
        parameter.Value.Should().BeOfType<int[]>().Which.Should().BeEmpty();
    }

    [Fact]
    public void EmptyEntityTableParameter_ShouldBindEmptyTupleArray()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("rows", Array.Empty<TvpRow>()));

        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Nullable(String)))");
        parameter.Value.Should().BeOfType<object?[][]>().Which.Should().BeEmpty();
    }

    [Fact]
    public void ScalarTableParameter_TimeOnly_ShouldFormatInvariantWithSubseconds()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "times",
            new[] { new TimeOnly(3, 4, 5, 123), new TimeOnly(23, 59, 59, 999) }));

        parameter.ClickHouseType.Should().Be("Array(String)");
        parameter.Value.Should().BeOfType<string[]>()
            .Which.Should().Equal("03:04:05.1230000", "23:59:59.9990000");
    }

    [Fact]
    public void NullableScalarTableParameter_TimeOnly_ShouldFormatInvariant()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "times",
            new TimeOnly?[] { new TimeOnly(3, 4, 5, 123), null }));

        parameter.ClickHouseType.Should().Be("Array(Nullable(String))");
        parameter.Value.Should().BeOfType<string?[]>()
            .Which.Should().Equal("03:04:05.1230000", null);
    }

    [Fact]
    public void ScalarTableParameter_TimeSpan_ShouldBindTicksConsistentlyWithEntity()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "durations",
            new[] { TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1) }));

        parameter.ClickHouseType.Should().Be("Array(Int64)");
        parameter.Value.Should().BeOfType<long[]>().Which.Should().Equal(600000000L, 10_000_000L);
    }

    [Fact]
    public void EntityTableParameter_TemporalColumns_ShouldMatchScalarRepresentation()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[]
            {
                new TemporalRow { Id = 1, At = new TimeOnly(3, 4, 5, 123), Elapsed = TimeSpan.FromMinutes(1) },
            }));

        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, String, Int64))");
        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows[0].Should().Equal(1, "03:04:05.1230000", 600000000L);
    }

    [Fact]
    public void EntityTableParameter_NullableTemporalColumns_ShouldBeNullable()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[] { new NullableTemporalRow { Id = 1, At = null, Elapsed = null } }));

        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Nullable(String), Nullable(Int64)))");
        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows[0].Should().Equal(1, null, null);
    }

    [Fact]
    public void ScalarTableParameter_ByteArray_ShouldBindStringElements()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "blobs",
            new[] { new byte[] { 1, 2, 3 }, Array.Empty<byte>() }));

        parameter.ClickHouseType.Should().Be("Array(Nullable(String))");
        var values = parameter.Value.Should().BeOfType<byte[][]>().Subject;
        values[0].Should().Equal(1, 2, 3);
        values[1].Should().BeEmpty();
    }

    [Fact]
    public void EntityTableParameter_ByteArrayColumn_ShouldBindStringElement()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[] { new BlobRow { Id = 1, Payload = new byte[] { 1, 2, 3 } } }));

        parameter.ClickHouseType.Should().Be("Array(Tuple(Int32, Nullable(String)))");
        var rows = parameter.Value.Should().BeOfType<object?[][]>().Subject;
        rows[0][0].Should().Be(1);
        rows[0][1].Should().BeOfType<byte[]>().Which.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void EntityTableParameter_NestedEntityProperty_ShouldThrowNotSupported()
    {
        using var ctx = new ExposedContext();

        var act = () => ctx.Create(ProcedureParameter.Table("rows", new[] { new NestedRow() }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TypeName_OnClickHouse_ShouldThrowArgumentException()
    {
        using var ctx = new ExposedContext();

        var act = () => ctx.Create(ProcedureParameter.Table("p", "dbo.MyType", new[] { 1 }));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NonInputTableParameter_ShouldThrowArgumentException()
    {
        using var ctx = new ExposedContext();
        var parameter = ProcedureParameter.Table("p", new[] { 1 }) with { Direction = ParameterDirection.Output };

        var act = () => ctx.Create(parameter);

        act.Should().Throw<ArgumentException>();
    }

    // Core's scalar ToArray widens sbyte/ushort/uint/ulong for PostgreSQL's array element types, but
    // ClickHouse binds those CLR types natively, so the scalar parameter type must equal the entity
    // column type and the bound array must carry the native element type.
    [Fact]
    public void ScalarNativeTypes_ShouldBindNativeArraysMatchingEntityColumns()
    {
        using var ctx = new ExposedContext();

        var entity = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            new[] { new NativeScalarRow { I8 = -1, U16 = 2, U32 = 3, U64 = 4, Ch = 'x' } }));
        entity.ClickHouseType.Should().Be("Array(Tuple(Int8, UInt16, UInt32, UInt64, String))");

        var i8 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new sbyte[] { -1, 127 }));
        var u16 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new ushort[] { 2, 65535 }));
        var u32 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new uint[] { 3, uint.MaxValue }));
        var u64 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new ulong[] { 4, ulong.MaxValue }));
        var ch = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new[] { 'x' }));

        i8.ClickHouseType.Should().Be("Array(Int8)");
        u16.ClickHouseType.Should().Be("Array(UInt16)");
        u32.ClickHouseType.Should().Be("Array(UInt32)");
        u64.ClickHouseType.Should().Be("Array(UInt64)");
        ch.ClickHouseType.Should().Be("Array(String)");

        i8.Value.Should().BeOfType<sbyte[]>().Which.Should().Equal((sbyte)-1, (sbyte)127);
        u16.Value.Should().BeOfType<ushort[]>().Which.Should().Equal((ushort)2, ushort.MaxValue);
        u32.Value.Should().BeOfType<uint[]>().Which.Should().Equal(3u, uint.MaxValue);
        u64.Value.Should().BeOfType<ulong[]>().Which.Should().Equal(4ul, ulong.MaxValue);
        ch.Value.Should().BeOfType<string[]>().Which.Should().Equal("x");
    }

    [Fact]
    public void ScalarNativeTypes_Nullable_ShouldBindNullableNativeArrays()
    {
        using var ctx = new ExposedContext();

        var i8 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new sbyte?[] { (sbyte)-1, null }));
        var u16 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new ushort?[] { (ushort)2, null }));
        var u32 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new uint?[] { 3u, null }));
        var u64 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new ulong?[] { 4ul, null }));
        var ch = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new char?[] { 'x', null }));

        i8.ClickHouseType.Should().Be("Array(Nullable(Int8))");
        u16.ClickHouseType.Should().Be("Array(Nullable(UInt16))");
        u32.ClickHouseType.Should().Be("Array(Nullable(UInt32))");
        u64.ClickHouseType.Should().Be("Array(Nullable(UInt64))");
        ch.ClickHouseType.Should().Be("Array(Nullable(String))");

        i8.Value.Should().BeOfType<sbyte?[]>().Which.Should().Equal((sbyte)-1, null);
        u16.Value.Should().BeOfType<ushort?[]>().Which.Should().Equal((ushort)2, null);
        u32.Value.Should().BeOfType<uint?[]>().Which.Should().Equal(3u, null);
        u64.Value.Should().BeOfType<ulong?[]>().Which.Should().Equal(4ul, null);
        ch.Value.Should().BeOfType<string[]>().Which.Should().Equal("x", null);
    }

    [Fact]
    public void ScalarNativeTypes_Empty_ShouldBindNativeEmptyArray()
    {
        using var ctx = new ExposedContext();

        var i8 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", Array.Empty<sbyte>()));
        var u64 = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", Array.Empty<ulong>()));

        i8.ClickHouseType.Should().Be("Array(Int8)");
        u64.ClickHouseType.Should().Be("Array(UInt64)");
        i8.Value.Should().BeOfType<sbyte[]>().Which.Should().BeEmpty();
        u64.Value.Should().BeOfType<ulong[]>().Which.Should().BeEmpty();
    }

    [Fact]
    public void ScalarChar_NonNullable_ShouldNotBeWrappedInNullable()
    {
        using var ctx = new ExposedContext();

        var parameter = (ClickHouseDbParameter)ctx.Create(ProcedureParameter.Table("v", new[] { 'a', 'b' }));

        parameter.ClickHouseType.Should().Be("Array(String)");
        parameter.Value.Should().BeOfType<string[]>().Which.Should().Equal("a", "b");
    }
}
