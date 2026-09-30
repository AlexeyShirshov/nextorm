using System.Data;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ClickHouse.Driver.Numerics;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// The ClickHouse driver reports a <c>Decimal(38, 10)</c> column as
/// <see cref="ClickHouseDecimal"/> (with its default <c>UseBigDecimal = true</c>), not as
/// <see cref="decimal"/>. The JSON row writer must accept that provider representation and read it
/// through the typed <see cref="IDataRecord.GetDecimal"/> accessor — the same accessor the buffered
/// row mapper uses for a <c>decimal</c> projection — instead of failing the numeric whitelist.
/// The end-to-end parity is covered by <c>ClickHouseIntegrationTests.WriteJson_TypedColumns</c>.
/// </summary>
public class JsonRowWriterFactoryClickHouseDecimalTests
{
    private sealed class ClickHouseDecimalRecord : IDataRecord
    {
        private readonly decimal _value;

        public ClickHouseDecimalRecord(decimal value) => _value = value;

        public int FieldCount => 1;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => "Decimal(38, 10)";

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => _value;

        public double GetDouble(int i) => throw new NotSupportedException();

        public Type GetFieldType(int i) => typeof(ClickHouseDecimal);

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetName(int i) => "Amount";

        public int GetOrdinal(string name) => 0;

        public string GetString(int i) => throw new NotSupportedException();

        public object GetValue(int i) => _value;

        public int GetValues(object[] values)
        {
            values[0] = _value;
            return 1;
        }

        public bool IsDBNull(int i) => false;
    }

    [Fact]
    public void Build_WithClickHouseDecimalFieldType_ShouldReadThroughGetDecimal()
    {
        // A ClickHouseDecimal(125, 1) value is decimal 12.5, matching the provider value a real
        // Decimal(38, 10) column hands back.
        var providerValue = new ClickHouseDecimal(new BigInteger(125), 1).ToDecimal(CultureInfo.InvariantCulture);
        providerValue.Should().Be(12.5m);

        var plan = JsonShapePlan.Build(
            [new SelectExpression(typeof(decimal)) { Index = 0, PropertyName = "Amount" }],
            oneColumn: false,
            new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writeRow(new ClickHouseDecimalRecord(providerValue), writer);
            writer.WriteEndArray();
        }

        Encoding.UTF8.GetString(stream.ToArray())
            .Should().Be(JsonSerializer.Serialize(new[] { new { Amount = 12.5m } }));
    }
}
