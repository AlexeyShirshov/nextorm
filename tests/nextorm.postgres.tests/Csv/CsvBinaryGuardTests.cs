using System.Collections;
using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// DB-free guard for the PostgreSQL CSV binary path. Npgsql streams LOBs, and its dialect declares
/// sequential access with no trailing locator, so a direct <c>byte[]</c> column on a confirmed
/// sequential reader must be admitted to the bounded chunked path (and stay buffered otherwise). The
/// real cross-provider behavior is asserted by the container-backed CSV and LOB integration suites.
/// </summary>
public class CsvBinaryGuardTests
{
    [Fact]
    public void PostgresBinary_OnSequentialReader_AdmitsBoundedChunking()
    {
        PostgresDialect.Instance.SupportsSequentialAccess.Should().BeTrue();
        PostgresDialect.Instance.LobLocatorColumn.Should().BeNull("Npgsql streams the payload without a locator");

        var ctx = PostgresTestContext.CreatePostgres();
        // FIX 2: the no-Expression mapper seam is admitted only on positive stored-column evidence, so the
        // hand-built probe must carry the mapping-derived physical column name of a real entity column.
        var column = new SelectExpression(typeof(byte[])) { Index = 0, PropertyName = "Data", PhysicalColumnName = "data" };
        var schema = new BinarySchemaRecord();
        var options = new CsvStreamOptions();

        var sequential = CsvStreamWriter.Build(
            [column],
            schema,
            (c, r, _) => ctx.MapColumnExpression(c, r),
            sequentialAccess: true,
            options);
        sequential.Columns[0].BinaryOrdinal.Should().Be(0);
        sequential.Columns[0].Write.Should().BeNull("the chunked path must not compile a whole-field getter");

        var buffered = CsvStreamWriter.Build(
            [column],
            schema,
            (c, r, _) => ctx.MapColumnExpression(c, r),
            sequentialAccess: false,
            options);
        buffered.Columns[0].BinaryOrdinal.Should().BeNull();
        buffered.Columns[0].Write.Should().NotBeNull();
    }

    /// <summary>A one-column schema whose only meaningful member is the BLOB storage type.</summary>
    private sealed class BinarySchemaRecord : IDataRecord
    {
        public int FieldCount => 1;

        public object this[int i] => throw new NotSupportedException();

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => "bytea";

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => throw new NotSupportedException();

        public double GetDouble(int i) => throw new NotSupportedException();

        public Type GetFieldType(int i) => typeof(byte[]);

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetName(int i) => "Data";

        public int GetOrdinal(string name) => 0;

        public string GetString(int i) => throw new NotSupportedException();

        public object GetValue(int i) => throw new NotSupportedException();

        public int GetValues(object[] values) => throw new NotSupportedException();

        public bool IsDBNull(int i) => false;

        public T GetFieldValue<T>(int i) => throw new NotSupportedException();

        public IEnumerator GetEnumerator() => throw new NotSupportedException();
    }
}
