using System.Data;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Parity coverage for the direct JSON row writer: a flat shape of plain scalars is written by
/// already-compiled code instead of a compiled expression tree, so the two writers must produce
/// identical JSON, including the NULL object-member rule, the reader field-type dispatch (a column may
/// surface wider than its declared CLR type) and the scalar form. Shapes the direct writer does not
/// reproduce exactly — Base64, enum-as-string, default-on-null and any captured recursive shape — must
/// be declined and keep the expression path.
/// </summary>
public class JsonDirectWriterTests
{
    [Fact]
    public void FlatShape_ShouldBeWrittenDirectlyAndMatchTheExpressionWriter()
    {
        var plan = FlatPlan();
        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeTrue();

        var present = new Row(
            [42, "Anna, \"A\"", 12.34m, true, Guid.Parse("d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e"), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)],
            [typeof(int), typeof(string), typeof(decimal), typeof(bool), typeof(Guid), typeof(DateTime)]);

        var expected = "{\"Id\":42,\"Name\":\"Anna, \\u0022A\\u0022\",\"Total\":12.34,\"Active\":true,"
            + "\"Key\":\"d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e\",\"Created\":\"2024-01-02T03:04:05Z\"}";
        Write(JsonRowWriterFactory.Build(plan), present).Should().Be(expected);
        Write(JsonRowWriterFactory.Build(plan), present).Should().Be(Write(JsonRowWriterFactory.Build(plan, allowDirectWriters: false), present));
    }

    [Fact]
    public void NullMember_ShouldFollowIgnoreNullInBothWriters()
    {
        var included = FlatPlan();
        var ignored = FlatPlan(new JsonStreamOptions { IgnoreNull = true });
        var row = new Row(
            [null, null, null, null, null, null],
            [typeof(int), typeof(string), typeof(decimal), typeof(bool), typeof(Guid), typeof(DateTime)]);

        const string allNull = "{\"Id\":null,\"Name\":null,\"Total\":null,\"Active\":null,\"Key\":null,\"Created\":null}";

        Write(JsonRowWriterFactory.Build(included), row).Should().Be(allNull);
        Write(JsonRowWriterFactory.Build(included), row).Should().Be(Write(JsonRowWriterFactory.Build(included, allowDirectWriters: false), row));

        Write(JsonRowWriterFactory.Build(ignored), row).Should().Be("{}");
        Write(JsonRowWriterFactory.Build(ignored), row).Should().Be(Write(JsonRowWriterFactory.Build(ignored, allowDirectWriters: false), row));
    }

    public static TheoryData<Type, Type, object?, string> NumericFieldTypes() => new()
    {
        // Declared CLR type, provider field type, stored value, expected JSON.
        { typeof(int), typeof(long), 42L, "42" },
        { typeof(int), typeof(short), (short)42, "42" },
        { typeof(long), typeof(int), 42, "42" },
        { typeof(byte), typeof(long), 200L, "200" },
        { typeof(short), typeof(decimal), 12.9m, "13" },
        { typeof(float), typeof(decimal), 1.5m, "1.5" },
        { typeof(double), typeof(decimal), 2.5m, "2.5" },
        { typeof(decimal), typeof(int), 3, "3" },
        { typeof(decimal), typeof(double), 12.34m, "12.34" },
        { typeof(int), typeof(uint), 4_000u, "4000" },
    };

    [Theory]
    [MemberData(nameof(NumericFieldTypes))]
    public void NumericColumn_ShouldDispatchOnTheProviderFieldTypeLikeTheExpressionWriter(
        Type declared, Type fieldType, object? storedValue, string expectedJson)
    {
        var column = new SelectExpression(declared) { Index = 0, PropertyName = "V" };
        var plan = JsonShapePlan.Build([column], oneColumn: false, new JsonStreamOptions());
        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeTrue();

        var row = new Row([storedValue], [fieldType]);
        Write(JsonRowWriterFactory.Build(plan), row).Should().Be("{\"V\":" + expectedJson + "}");
        Write(JsonRowWriterFactory.Build(plan), row).Should().Be(Write(JsonRowWriterFactory.Build(plan, allowDirectWriters: false), row));
    }

    [Fact]
    public void ScalarPlan_ShouldBeWrittenDirectlyAndMatchTheExpressionWriter()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V" };
        var plan = JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());
        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeTrue();

        Write(JsonRowWriterFactory.Build(plan), new Row([7], [typeof(int)])).Should().Be("7");
        Write(JsonRowWriterFactory.Build(plan), new Row([null], [typeof(int)])).Should().Be("null");
        Write(JsonRowWriterFactory.Build(plan), new Row([7], [typeof(int)]))
            .Should().Be(Write(JsonRowWriterFactory.Build(plan, allowDirectWriters: false), new Row([7], [typeof(int)])));
    }

    [Fact]
    public void ByteArrayColumn_ShouldKeepTheExpressionWriter()
    {
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "Data" };
        var plan = JsonShapePlan.Build([column], oneColumn: false, new JsonStreamOptions());

        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeFalse("Base64 writes through a DbDataReader typed accessor, not a plain getter");
    }

    [Fact]
    public void DefaultOnNullColumn_ShouldKeepTheExpressionWriter()
    {
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "V", DefaultOnNull = true };
        var plan = JsonShapePlan.Build([column], oneColumn: false, new JsonStreamOptions());

        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeFalse("a SQL NULL must be emitted as default(T), which the direct writer does not substitute");
    }

    [Fact]
    public void RecursiveShape_ShouldKeepTheExpressionWriter()
    {
        var shape = JsonShapeNode.Object(
            null,
            typeof(object),
            [JsonShapeNode.Scalar("Id", typeof(int), new JsonShapeBinding(0, nullable: false, defaultOnNull: false))],
            JsonShapePresence.Always,
            slot: null,
            member: null);
        var column = new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" };
        var plan = JsonShapePlan.Build([column], oneColumn: false, new JsonStreamOptions(), shape);

        JsonRowWriterFactory.CanWriteDirectly(plan).Should().BeFalse("a captured recursive shape carries its own descriptor and writer");
    }

    private static JsonShapePlan FlatPlan(JsonStreamOptions? options = null)
    {
        var columns = new SelectExpression[]
        {
            new(typeof(int)) { Index = 0, PropertyName = "Id" },
            new(typeof(string)) { Index = 1, PropertyName = "Name" },
            new(typeof(decimal)) { Index = 2, PropertyName = "Total" },
            new(typeof(bool)) { Index = 3, PropertyName = "Active" },
            new(typeof(Guid)) { Index = 4, PropertyName = "Key" },
            new(typeof(DateTime)) { Index = 5, PropertyName = "Created" },
        };

        return JsonShapePlan.Build(columns, oneColumn: false, options ?? new JsonStreamOptions());
    }

    private static string Write(JsonRowWriter rowWriter, IDataRecord record)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            rowWriter(record, writer);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A record whose values and provider field types are declared independently.</summary>
    private sealed class Row : IDataRecord
    {
        private readonly object?[] _values;
        private readonly Type[] _types;

        public Row(object?[] values, Type[] types)
        {
            _values = values;
            _types = types;
        }

        public int FieldCount => _values.Length;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)_values[i]!;

        public byte GetByte(int i) => (byte)_values[i]!;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => _types[i].Name;

        public DateTime GetDateTime(int i) => (DateTime)_values[i]!;

        public decimal GetDecimal(int i) => (decimal)_values[i]!;

        public double GetDouble(int i) => (double)_values[i]!;

        public Type GetFieldType(int i) => _types[i];

        public float GetFloat(int i) => (float)_values[i]!;

        public Guid GetGuid(int i) => (Guid)_values[i]!;

        public short GetInt16(int i) => (short)_values[i]!;

        public int GetInt32(int i) => (int)_values[i]!;

        public long GetInt64(int i) => (long)_values[i]!;

        public string GetName(int i) => $"c{i}";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => (string)_values[i]!;

        public object GetValue(int i) => _values[i] ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);
            return count;
        }

        public bool IsDBNull(int i) => _values[i] is null;
    }
}
