using System.Data;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Focused binding tests of the SQL Server table-valued parameter path: the descriptor is turned into a
/// structured <see cref="SqlParameter"/> with the user-defined table type name and a streamed record
/// set, without a server (the placeholder connection string is never used). The CLR-type matrix pins the
/// metadata SQL Server receives (the record's <see cref="SqlMetaData"/>) plus the value actually streamed,
/// where a widening type is asserted through its coerced representation rather than the CLR input.
/// </summary>
public class TableValuedParameterBindingTests
{
    private const string TypeName = "dbo.TvpShape";

    private static readonly MethodInfo TableWithTypeNameMethod = typeof(ProcedureParameter)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(ProcedureParameter.Table) && m.GetParameters().Length == 3);

    private sealed class TvpRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class MixedEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Amount { get; set; }
        public Guid Key { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public byte[]? Payload { get; set; }
        public Int32Enum Kind { get; set; }
        public TimeSpan Duration { get; set; }
        public int? Optional { get; set; }
    }

    private enum Int32Enum
    {
        Zero = 0,
        Answer = 42,
    }

    private enum Int64Enum : long
    {
        Big = 5_000_000_000L,
    }

    private sealed class DecimalTvpRow
    {
        public int Id { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal Amount { get; set; }

        public decimal Plain { get; set; }

        [DecimalPrecision(12, 4)]
        public decimal? OptionalAmount { get; set; }
    }

    // Registered fluently before first use: the process-wide metadata cache then feeds the TVP binder.
    private sealed class FluentDecimalTvpRow
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class ZeroPrecisionTvpRow
    {
        [DecimalPrecision(0, 0)]
        public decimal Amount { get; set; }
    }

    private sealed class PrecisionOutOfRangeTvpRow
    {
        [DecimalPrecision(39, 38)]
        public decimal Amount { get; set; }
    }

    private sealed class ScaleOverPrecisionTvpRow
    {
        [DecimalPrecision(10, 11)]
        public decimal Amount { get; set; }
    }

    private sealed class ExposedContext : SqlServerDataContext
    {
        public ExposedContext()
            : base(
                "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
                new DataContextBuilder())
        {
        }

        public DbParameter Create(ProcedureParameter parameter) => CreateProcedureParameter(parameter);
    }

    /// <summary>
    /// One row per CLR type reaching <c>SqlMetaData</c>: the expected SQL type (and the max length /
    /// precision / scale where the mapping declares one) plus the value that must appear in the record.
    /// The expected value is the widened/coerced wire value, so a regression in the conversion (for
    /// example <c>ulong</c> no longer becoming <c>decimal</c>) fails here rather than on a server.
    /// </summary>
    public static TheoryData<Type, SqlDbType, long?, byte?, byte?, object, object> ScalarCases => new()
    {
        // Boolean and character.
        { typeof(bool), SqlDbType.Bit, null, null, null, true, true },
        { typeof(char), SqlDbType.NChar, 1L, null, null, 'x', "x" },

        // The widening integer family: each signed/unsigned type is bound through a type SQL Server has.
        { typeof(sbyte), SqlDbType.SmallInt, null, null, null, (sbyte)-5, (short)-5 },
        { typeof(byte), SqlDbType.TinyInt, null, null, null, (byte)200, (byte)200 },
        { typeof(short), SqlDbType.SmallInt, null, null, null, (short)-300, (short)-300 },
        { typeof(ushort), SqlDbType.Int, null, null, null, (ushort)60_000, 60_000 },
        { typeof(int), SqlDbType.Int, null, null, null, 42, 42 },
        { typeof(uint), SqlDbType.BigInt, null, null, null, 4_000_000_000u, 4_000_000_000L },
        { typeof(long), SqlDbType.BigInt, null, null, null, -9_000_000_000L, -9_000_000_000L },
        { typeof(ulong), SqlDbType.Decimal, null, (byte)20, (byte)0, 18_000_000_000_000_000_000UL, 18_000_000_000_000_000_000m },

        // Floating point and decimal.
        { typeof(float), SqlDbType.Real, null, null, null, 1.5f, 1.5f },
        { typeof(double), SqlDbType.Float, null, null, null, 2.5d, 2.5d },
        { typeof(decimal), SqlDbType.Decimal, null, (byte)38, (byte)18, 123.456m, 123.456m },

        // Text and binary use max-length types.
        { typeof(string), SqlDbType.NVarChar, -1L, null, null, "hello", "hello" },
        { typeof(byte[]), SqlDbType.VarBinary, -1L, null, null, new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 } },

        // Temporal. DateOnly/TimeOnly surface through the DateTime/TimeSpan provider representation.
        { typeof(DateTime), SqlDbType.DateTime2, null, null, null, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc) },
        { typeof(DateTimeOffset), SqlDbType.DateTimeOffset, null, null, null, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)), new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)) },
        { typeof(DateOnly), SqlDbType.Date, null, null, null, new DateOnly(2024, 1, 2), new DateTime(2024, 1, 2) },
        { typeof(TimeOnly), SqlDbType.Time, null, null, null, new TimeOnly(3, 4, 5), new TimeOnly(3, 4, 5).ToTimeSpan() },
        { typeof(TimeSpan), SqlDbType.BigInt, null, null, null, TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(90).Ticks },

        // Miscellaneous scalars, including an enum bound through its underlying numeric type.
        { typeof(Guid), SqlDbType.UniqueIdentifier, null, null, null, Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") },
        { typeof(Int32Enum), SqlDbType.Int, null, null, null, Int32Enum.Answer, 42 },
        { typeof(Int64Enum), SqlDbType.BigInt, null, null, null, Int64Enum.Big, 5_000_000_000L },
    };

    /// <summary>Nullable scalar row types whose single row is SQL <c>NULL</c> (still one typed column).</summary>
    public static TheoryData<Type, SqlDbType> NullableScalarCases => new()
    {
        { typeof(bool?), SqlDbType.Bit },
        { typeof(int?), SqlDbType.Int },
        { typeof(decimal?), SqlDbType.Decimal },
        { typeof(Guid?), SqlDbType.UniqueIdentifier },
        { typeof(DateTime?), SqlDbType.DateTime2 },
        { typeof(TimeOnly?), SqlDbType.Time },
    };

    /// <summary>Scalar row types bound with an empty set: metadata is still built, the value is unset.</summary>
    public static TheoryData<Type, SqlDbType> EmptyScalarCases => new()
    {
        { typeof(int), SqlDbType.Int },
        { typeof(Guid), SqlDbType.UniqueIdentifier },
        { typeof(byte[]), SqlDbType.VarBinary },
    };

    [Theory]
    [MemberData(nameof(ScalarCases), DisableDiscoveryEnumeration = true)]
    public void ScalarTableParameter_ShouldMapClrTypeToMetadataAndRecordValue(
        Type rowType,
        SqlDbType expectedDbType,
        long? expectedMaxLength,
        byte? expectedPrecision,
        byte? expectedScale,
        object value,
        object expectedRecordValue)
    {
        using var ctx = new ExposedContext();

        var parameter = (SqlParameter)ctx.Create(BuildScalarTableParameter(rowType, value));
        var record = SingleRecord(parameter);
        var metadata = record.GetSqlMetaData(0);

        metadata.SqlDbType.Should().Be(expectedDbType);
        if (expectedMaxLength is { } maxLength)
            metadata.MaxLength.Should().Be(maxLength);
        if (expectedPrecision is { } precision)
            metadata.Precision.Should().Be(precision);
        if (expectedScale is { } scale)
            metadata.Scale.Should().Be(scale);

        AssertRecordValue(record.GetValue(0), expectedRecordValue);
    }

    [Theory]
    [MemberData(nameof(NullableScalarCases), DisableDiscoveryEnumeration = true)]
    public void NullableScalarTableParameter_WithNullValue_ShouldBindDBNull(Type rowType, SqlDbType expectedDbType)
    {
        using var ctx = new ExposedContext();

        var parameter = (SqlParameter)ctx.Create(BuildScalarTableParameter(rowType, null));
        var record = SingleRecord(parameter);

        record.GetSqlMetaData(0).SqlDbType.Should().Be(expectedDbType);
        record.IsDBNull(0).Should().BeTrue();
        record.GetValue(0).Should().Be(DBNull.Value);
    }

    [Theory]
    [MemberData(nameof(EmptyScalarCases), DisableDiscoveryEnumeration = true)]
    public void EmptyScalarTableParameter_ShouldBindNullValueWithMetadataBuilt(Type rowType, SqlDbType expectedDbType)
    {
        using var ctx = new ExposedContext();

        var parameter = (SqlParameter)ctx.Create(BuildEmptyScalarTableParameter(rowType));

        // SqlClient rejects both DBNull and an empty record sequence for a structured parameter, so an
        // empty set is bound as an unset value; the type name is still applied.
        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be(TypeName);
        parameter.Value.Should().BeNull();
        expectedDbType.Should().NotBe(SqlDbType.Structured);
    }

    [Fact]
    public void EntityTableParameter_ShouldBuildPerColumnMetadataAndStreamValues()
    {
        using var ctx = new ExposedContext();
        var key = Guid.NewGuid();
        var row = new MixedEntity
        {
            Id = 7,
            Name = "alpha",
            Amount = 12.5m,
            Key = key,
            Date = new DateOnly(2024, 1, 2),
            Time = new TimeOnly(3, 4, 5),
            Payload = new byte[] { 9, 8, 7 },
            Kind = Int32Enum.Answer,
            Duration = TimeSpan.FromMinutes(90),
            Optional = null,
        };

        var parameter = (SqlParameter)ctx.Create(ProcedureParameter.Table("rows", TypeName, new[] { row }));
        var record = SingleRecord(parameter);

        var id = IndexOf(record, nameof(MixedEntity.Id));
        record.GetSqlMetaData(id).SqlDbType.Should().Be(SqlDbType.Int);
        record.GetInt32(id).Should().Be(7);

        var name = IndexOf(record, nameof(MixedEntity.Name));
        record.GetSqlMetaData(name).SqlDbType.Should().Be(SqlDbType.NVarChar);
        record.GetString(name).Should().Be("alpha");

        var amount = IndexOf(record, nameof(MixedEntity.Amount));
        record.GetSqlMetaData(amount).SqlDbType.Should().Be(SqlDbType.Decimal);
        record.GetSqlMetaData(amount).Precision.Should().Be(38);
        record.GetSqlMetaData(amount).Scale.Should().Be(18);
        record.GetDecimal(amount).Should().Be(12.5m);

        var keyIndex = IndexOf(record, nameof(MixedEntity.Key));
        record.GetSqlMetaData(keyIndex).SqlDbType.Should().Be(SqlDbType.UniqueIdentifier);
        record.GetGuid(keyIndex).Should().Be(key);

        var date = IndexOf(record, nameof(MixedEntity.Date));
        record.GetSqlMetaData(date).SqlDbType.Should().Be(SqlDbType.Date);
        record.GetDateTime(date).Should().Be(new DateTime(2024, 1, 2));

        var time = IndexOf(record, nameof(MixedEntity.Time));
        record.GetSqlMetaData(time).SqlDbType.Should().Be(SqlDbType.Time);
        record.GetValue(time).Should().Be(new TimeOnly(3, 4, 5).ToTimeSpan());

        var payload = IndexOf(record, nameof(MixedEntity.Payload));
        record.GetSqlMetaData(payload).SqlDbType.Should().Be(SqlDbType.VarBinary);
        ((byte[])record.GetValue(payload)).Should().Equal(new byte[] { 9, 8, 7 });

        var kind = IndexOf(record, nameof(MixedEntity.Kind));
        record.GetSqlMetaData(kind).SqlDbType.Should().Be(SqlDbType.Int);
        record.GetInt32(kind).Should().Be(42);

        var duration = IndexOf(record, nameof(MixedEntity.Duration));
        record.GetSqlMetaData(duration).SqlDbType.Should().Be(SqlDbType.BigInt);
        record.GetInt64(duration).Should().Be(TimeSpan.FromMinutes(90).Ticks);

        var optional = IndexOf(record, nameof(MixedEntity.Optional));
        record.GetSqlMetaData(optional).SqlDbType.Should().Be(SqlDbType.Int);
        record.IsDBNull(optional).Should().BeTrue();
    }

    [Fact]
    public void EntityTableParameter_DecimalPrecision_ShouldUseDeclaredAndDefaultMetadata()
    {
        using var ctx = new ExposedContext();
        var row = new DecimalTvpRow { Id = 1, Amount = 12.3456m, Plain = 1.5m, OptionalAmount = null };

        var parameter = (SqlParameter)ctx.Create(ProcedureParameter.Table("rows", TypeName, new[] { row }));
        var record = SingleRecord(parameter);

        var amount = IndexOf(record, nameof(DecimalTvpRow.Amount));
        record.GetSqlMetaData(amount).SqlDbType.Should().Be(SqlDbType.Decimal);
        record.GetSqlMetaData(amount).Precision.Should().Be(12);
        record.GetSqlMetaData(amount).Scale.Should().Be(4);
        record.GetDecimal(amount).Should().Be(12.3456m);

        // An unconfigured decimal column keeps the provider default (38, 18).
        var plain = IndexOf(record, nameof(DecimalTvpRow.Plain));
        record.GetSqlMetaData(plain).Precision.Should().Be(38);
        record.GetSqlMetaData(plain).Scale.Should().Be(18);

        // The declared precision/scale apply to a nullable column too and a null row keeps its metadata.
        var optional = IndexOf(record, nameof(DecimalTvpRow.OptionalAmount));
        record.GetSqlMetaData(optional).Precision.Should().Be(12);
        record.GetSqlMetaData(optional).Scale.Should().Be(4);
        record.IsDBNull(optional).Should().BeTrue();
    }

    [Fact]
    public void EntityTableParameter_FluentDecimalPrecision_ShouldUseRegisteredMetadata()
    {
        using var ctx = new ExposedContext();

        // The fluent registration populates the process-wide metadata cache; the TVP binder resolves the
        // row type by CLR type and must reuse it instead of auto-building the default (38, 18). The
        // delegate is typed explicitly because From<T> also has an Action<FromOptions> overload.
        Action<EntityMetadataBuilder<FluentDecimalTvpRow>> configure = b => b.Property(x => x.Amount).DecimalPrecision(12, 4);
        _ = ctx.From(configure);

        var parameter = (SqlParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            TypeName,
            new[] { new FluentDecimalTvpRow { Id = 1, Amount = 1.5m } }));
        var record = SingleRecord(parameter);

        var amount = IndexOf(record, nameof(FluentDecimalTvpRow.Amount));
        record.GetSqlMetaData(amount).Precision.Should().Be(12);
        record.GetSqlMetaData(amount).Scale.Should().Be(4);
        record.GetDecimal(amount).Should().Be(1.5m);
    }

    public static TheoryData<Type> InvalidDecimalMetadataRowTypes => new()
    {
        typeof(ZeroPrecisionTvpRow),
        typeof(PrecisionOutOfRangeTvpRow),
        typeof(ScaleOverPrecisionTvpRow),
    };

    [Theory]
    [MemberData(nameof(InvalidDecimalMetadataRowTypes), DisableDiscoveryEnumeration = true)]
    public void EntityTableParameter_InvalidDecimalMetadata_ShouldThrow(Type rowType)
    {
        using var ctx = new ExposedContext();
        var rows = Array.CreateInstance(rowType, 1);

        Action act = () => ctx.Create(InvokeTable(rowType, rows));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnsupportedScalarRowType_ShouldThrowNotSupportedException()
    {
        using var ctx = new ExposedContext();

        // IntPtr is treated as a scalar row type by the binder but has no SQL Server mapping, so the
        // metadata builder must reject it with the column name instead of streaming an untyped record.
        var act = () => ctx.Create(BuildScalarTableParameter(typeof(IntPtr), IntPtr.Zero));

        act.Should().Throw<NotSupportedException>().WithMessage("*IntPtr*");
    }

    [Fact]
    public void TableParameter_ShouldBindStructuredParameterWithTypeName()
    {
        using var ctx = new ExposedContext();

        var parameter = (SqlParameter)ctx.Create(ProcedureParameter.Table(
            "rows",
            "dbo.TvpRow",
            new[] { new TvpRow { Id = 1, Name = "alpha" } }));

        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be("dbo.TvpRow");
        parameter.Value.Should().BeAssignableTo<IEnumerable<SqlDataRecord>>();
    }

    [Fact]
    public void TableParameter_WithoutTypeName_ShouldThrowArgumentException()
    {
        using var ctx = new ExposedContext();

        var act = () => ctx.Create(ProcedureParameter.Table("rows", new[] { new TvpRow { Id = 1 } }));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmptyTableParameter_ShouldBindNullValue()
    {
        using var ctx = new ExposedContext();

        var parameter = (SqlParameter)ctx.Create(
            ProcedureParameter.Table("rows", "dbo.TvpRow", Array.Empty<TvpRow>()));

        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be("dbo.TvpRow");
        parameter.Value.Should().BeNull();
    }

    private static ProcedureParameter BuildScalarTableParameter(Type rowType, object? value)
    {
        var rows = Array.CreateInstance(rowType, 1);
        rows.SetValue(value, 0);
        return InvokeTable(rowType, rows);
    }

    private static ProcedureParameter BuildEmptyScalarTableParameter(Type rowType)
        => InvokeTable(rowType, Array.CreateInstance(rowType, 0));

    private static ProcedureParameter InvokeTable(Type rowType, Array rows)
        => (ProcedureParameter)TableWithTypeNameMethod
            .MakeGenericMethod(rowType)
            .Invoke(null, ["rows", TypeName, rows])!;

    private static SqlDataRecord SingleRecord(SqlParameter parameter)
    {
        parameter.SqlDbType.Should().Be(SqlDbType.Structured);
        parameter.TypeName.Should().Be(TypeName);

        var records = ((IEnumerable<SqlDataRecord>)parameter.Value!).ToList();
        records.Should().ContainSingle();
        return records[0];
    }

    private static int IndexOf(SqlDataRecord record, string columnName)
    {
        for (var i = 0; i < record.FieldCount; i++)
        {
            if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        var columns = string.Join(", ", Enumerable.Range(0, record.FieldCount).Select(i => record.GetName(i)));
        throw new InvalidOperationException($"Column '{columnName}' was not streamed; record columns: {columns}.");
    }

    private static void AssertRecordValue(object? actual, object expected)
    {
        if (expected is byte[] bytes)
        {
            actual.Should().BeAssignableTo<byte[]>().Which.Should().Equal(bytes);
            return;
        }

        actual.Should().Be(expected);
    }
}
