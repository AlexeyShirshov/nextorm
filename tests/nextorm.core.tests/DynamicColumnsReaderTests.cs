using System.Collections;
using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit tests for the internal dynamic-columns reader: it copies the mapped names on construction, so
/// mutating the caller's array afterwards cannot change which result fields are treated as mapped.
/// </summary>
public class DynamicColumnsReaderTests
{
    [Fact]
    public void Read_ShouldIgnoreMutationsOfTheMappedNamesArray()
    {
        var mappedNames = new[] { "name" };
        var reader = new DynamicColumns(mappedNames);

        // Mutate the caller-owned array after the reader was built. A defensive copy on construction
        // keeps "name" mapped and "id" unmapped; without the copy the mutation would hide "id" too.
        mappedNames[0] = "id";

        var result = reader.Read(new FixedRecord(("name", "Ann"), ("id", 1L)), 0);

        result.Should().ContainKey("id").WhoseValue.Should().Be(1L);
        result.Should().NotContainKey("name");
    }

    /// <summary>A minimal <see cref="IDataRecord"/> over a fixed list of name/value fields.</summary>
    private sealed class FixedRecord(params (string Name, object? Value)[] fields) : IDataRecord
    {
        public int FieldCount => fields.Length;

        public object this[int i] => fields[i].Value ?? DBNull.Value;

        public object this[string name] => this[GetOrdinal(name)];

        public bool IsDBNull(int i) => fields[i].Value is null or DBNull;

        public object GetValue(int i) => fields[i].Value ?? DBNull.Value;

        public string GetName(int i) => fields[i].Name;

        public int GetOrdinal(string name)
        {
            for (var i = 0; i < fields.Length; i++)
            {
                if (string.Equals(fields[i].Name, name, StringComparison.Ordinal))
                    return i;
            }

            throw new IndexOutOfRangeException(name);
        }

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, fields.Length);
            for (var i = 0; i < count; i++)
                values[i] = this[i];

            return count;
        }

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

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetString(int i) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public IEnumerator GetEnumerator() => fields.GetEnumerator();
    }
}
