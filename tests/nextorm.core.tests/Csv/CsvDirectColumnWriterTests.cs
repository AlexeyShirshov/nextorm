using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Parity coverage for the direct CSV column writer: a column whose mapped accessor is a plain
/// <see cref="IDataRecord"/> getter is written by already-compiled code instead of a compiled
/// expression tree, so the two writers must produce identical bytes for every supported scalar type,
/// for NULL and for the formatting policies. Under NativeAOT the expression writer is interpreted and
/// the direct one is not, which is why the shapes must be interchangeable rather than merely similar.
/// </summary>
public class CsvDirectColumnWriterTests
{
    private static Func<SelectExpression, Expression, Type, Expression> MapTyped
        => (column, record, _) => RowMapperFactory.MapColumn(column, record);

    private static readonly (Type Type, object? Value, string Expected)[] CaseList = BuildCases();

    private static (Type Type, object? Value, string Expected)[] BuildCases()
    {
        var date = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var guid = Guid.Parse("d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e");
        return
        [
            (typeof(bool), true, "true"),
            (typeof(byte), (byte)200, "200"),
            (typeof(short), (short)-1234, "-1234"),
            (typeof(int), 42, "42"),
            (typeof(long), -9_000_000_000L, "-9000000000"),
            (typeof(float), 1.5f, "1.5"),
            (typeof(double), 1.5d, "1.5"),
            (typeof(decimal), 1.25m, "1.25"),
            (typeof(Guid), guid, guid.ToString("D", CultureInfo.InvariantCulture)),
            (typeof(DateTime), date, date.ToString("O", CultureInfo.InvariantCulture)),
            (typeof(string), "hello, world", "\"hello, world\""),
            (typeof(int?), 7, "7"),
            (typeof(int?), null, "NULL"),
            (typeof(decimal?), 2.5m, "2.5"),
            (typeof(bool?), null, "NULL"),
            (typeof(Guid?), guid, guid.ToString("D", CultureInfo.InvariantCulture)),
            (typeof(DateTime?), null, "NULL"),
            (typeof(string), null, "NULL"),
            (typeof(string), string.Empty, ""),
        ];
    }

    public static TheoryData<Type, object?, string> Cases()
    {
        var data = new TheoryData<Type, object?, string>();
        foreach (var (type, value, expected) in CaseList)
            data.Add(type, value, expected);

        return data;
    }

    public static TheoryData<Type, object?> PolicyCases()
    {
        var data = new TheoryData<Type, object?>();
        foreach (var (type, value, _) in CaseList)
            data.Add(type, value);

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DirectWriter_ShouldMatchExpressionWriter(Type type, object? value, string expected)
    {
        var column = new SelectExpression(type) { Index = 0, PropertyName = "V" };
        var options = new CsvStreamOptions { IncludeHeader = false, NullMarker = "NULL" };

        var direct = CsvStreamWriter.Build([column], schema: null, MapTyped, options: options, allowDirectColumnWriters: true);
        var expression = CsvStreamWriter.Build([column], schema: null, MapTyped, options: options, allowDirectColumnWriters: false);

        direct.Columns[0].IsDirect.Should().BeTrue("the plain typed getter is exactly what the direct writer reproduces");
        expression.Columns[0].IsDirect.Should().BeFalse("the reference plan must stay on the expression path");

        var row = new ScalarRecord(value, type);
        WriteDirect(direct, row, options).Should().Be(expected + "\r\n");
        WriteDirect(direct, row, options).Should().Be(WriteDirect(expression, row, options));
    }

    [Theory]
    [MemberData(nameof(PolicyCases))]
    public void DirectWriter_ShouldHonourTheCallPolicy(Type type, object? value)
    {
        var column = new SelectExpression(type) { Index = 0, PropertyName = "V" };
        var options = new CsvStreamOptions
        {
            IncludeHeader = false,
            NullMarker = "<null>",
            ValueTransform = _ => "t",
        };

        var direct = CsvStreamWriter.Build([column], schema: null, MapTyped, options: options);
        var expression = CsvStreamWriter.Build([column], schema: null, MapTyped, options: options, allowDirectColumnWriters: false);

        var row = new ScalarRecord(value, type);
        var expectedByPolicy = value is null ? "<null>\r\n" : "t\r\n";
        WriteDirect(direct, row, options).Should().Be(expectedByPolicy);
        WriteDirect(direct, row, options).Should().Be(WriteDirect(expression, row, options));
    }

    [Fact]
    public void ByteArrayColumn_ShouldKeepTheExpressionWriter()
    {
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "Data" };
        var plan = CsvStreamWriter.Build([column], schema: null, MapTyped);

        plan.Columns[0].IsDirect.Should().BeFalse("byte[] is read through a GetFieldValue<byte[]> wrapper, not a plain getter");
    }

    [Fact]
    public void UIntColumn_ShouldKeepTheExpressionWriter()
    {
        var column = new SelectExpression(typeof(uint)) { Index = 0, PropertyName = "V" };
        var plan = CsvStreamWriter.Build([column], schema: null, MapTyped);

        plan.Columns[0].IsDirect.Should().BeFalse("uint has no typed IDataRecord getter");
    }

    [Fact]
    public void UnsupportedProjection_ShouldStillThrowBeforeAnyRow()
    {
        var column = new SelectExpression(typeof(object)) { Index = 0, PropertyName = "V" };

        FluentActions.Invoking(() => CsvStreamWriter.Build([column], schema: null, MapTyped))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot format the column 'V'*");
    }

    private static string WriteDirect(CsvPlan plan, IDataRecord record, CsvStreamOptions options)
    {
        using var buffer = new CsvRowBuffer(new CsvDialect(options.Delimiter), new CsvFieldPolicy(options.NullMarker, options.ExcelMode, options.ValueTransform));
        CsvStreamWriter.WriteRow(plan, record, buffer);
        return Encoding.UTF8.GetString(buffer.Written);
    }

    /// <summary>A single-column record whose typed getters cast the stored value, with DBNull for null.</summary>
    private sealed class ScalarRecord : IDataRecord
    {
        private readonly object? _value;
        private readonly Type _type;

        public ScalarRecord(object? value, Type type)
        {
            _value = value;
            _type = Nullable.GetUnderlyingType(type) ?? type;
        }

        public int FieldCount => 1;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)_value!;

        public byte GetByte(int i) => (byte)_value!;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => _type.Name;

        public DateTime GetDateTime(int i) => (DateTime)_value!;

        public decimal GetDecimal(int i) => (decimal)_value!;

        public double GetDouble(int i) => (double)_value!;

        public Type GetFieldType(int i) => _type;

        public float GetFloat(int i) => (float)_value!;

        public Guid GetGuid(int i) => (Guid)_value!;

        public short GetInt16(int i) => (short)_value!;

        public int GetInt32(int i) => (int)_value!;

        public long GetInt64(int i) => (long)_value!;

        public string GetName(int i) => "V";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => (string)_value!;

        public object GetValue(int i) => _value ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            values[0] = GetValue(0);
            return 1;
        }

        public bool IsDBNull(int i) => _value is null;
    }
}
