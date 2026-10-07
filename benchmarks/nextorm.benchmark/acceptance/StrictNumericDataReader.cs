using System.Collections;
using System.Data;
using System.Data.Common;

namespace NextORM.Benchmark.Acceptance;

/// <summary>
/// A strict, allocation-instrumented <see cref="DbDataReader"/> that mimics SqlClient's typed getters:
/// a typed getter throws <see cref="InvalidCastException"/> when the requested CLR type is not the
/// column's declared storage type. This is the reader the acceptance benchmark materializes against, so
/// the object-based legacy path (<c>GetValue</c> + <c>Convert.ChangeType</c>) must call
/// <see cref="GetValue"/>, while the typed runtime-dispatch path must never call it.
/// </summary>
/// <remarks>
/// <see cref="GetValue"/> always returns a <b>freshly boxed</b> value (a new box per call), exactly as a
/// value-type column does through an ADO.NET object accessor, so the per-row allocation of the legacy
/// path is measured rather than accidentally satisfied by a cached box. <see cref="GetValueCalls"/>
/// is the definitive "numeric-source boxing count" for the gate: it must be zero for the candidate.
/// </remarks>
internal sealed class StrictNumericDataReader : DbDataReader
{
    private readonly object?[] _values;
    private readonly Type[] _storage;
    private readonly bool[] _isNull;

    public StrictNumericDataReader(object?[] values, Type[] storage, bool[] isNull)
    {
        _values = values;
        _storage = storage;
        _isNull = isNull;
    }

    /// <summary>Number of times the object accessor <see cref="GetValue"/> was called (the boxing count).</summary>
    public long GetValueCalls { get; private set; }

    /// <summary>Number of successful typed getter reads (sanity check that the typed path ran).</summary>
    public long TypedReadCalls { get; private set; }

    public override int FieldCount => _values.Length;

    public override bool HasRows => true;

    public override bool IsClosed => false;

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override int VisibleFieldCount => _values.Length;

    public override object this[int ordinal] => throw new NotSupportedException();

    public override object this[string name] => throw new NotSupportedException();

    public override object GetValue(int ordinal)
    {
        GetValueCalls++;

        // A fresh box per call: the switch's pattern variable is a new local, so `(object)v` allocates
        // a new box even though the backing array already holds a boxed copy.
        return _values[ordinal] switch
        {
            null => DBNull.Value,
            DBNull => DBNull.Value,
            int v => v,
            long v => v,
            short v => v,
            byte v => v,
            float v => v,
            double v => v,
            decimal v => v,
            var other => other,
        };
    }

    public override bool IsDBNull(int ordinal) => _isNull[ordinal];

    public override Type GetFieldType(int ordinal) => _storage[ordinal];

    public override string GetDataTypeName(int ordinal) => _storage[ordinal].Name;

    public override string GetName(int ordinal) => $"c{ordinal}";

    public override int GetOrdinal(string name) => throw new NotSupportedException();

    public override bool Read() => throw new NotSupportedException();

    public override bool NextResult() => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();

    public override int GetValues(object[] values) => throw new NotSupportedException();

    public override bool GetBoolean(int ordinal) => Require<bool>(ordinal);

    public override byte GetByte(int ordinal) => Require<byte>(ordinal);

    public override short GetInt16(int ordinal) => Require<short>(ordinal);

    public override int GetInt32(int ordinal) => Require<int>(ordinal);

    public override long GetInt64(int ordinal) => Require<long>(ordinal);

    public override float GetFloat(int ordinal) => Require<float>(ordinal);

    public override double GetDouble(int ordinal) => Require<double>(ordinal);

    public override decimal GetDecimal(int ordinal) => Require<decimal>(ordinal);

    public override string GetString(int ordinal) => Require<string>(ordinal);

    public override Guid GetGuid(int ordinal) => Require<Guid>(ordinal);

    public override DateTime GetDateTime(int ordinal) => Require<DateTime>(ordinal);

    public override char GetChar(int ordinal) => Require<char>(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    private T Require<T>(int ordinal)
    {
        if (ordinal < 0 || ordinal >= _storage.Length)
            throw new IndexOutOfRangeException($"Ordinal {ordinal} is out of range.");

        if (_storage[ordinal] != typeof(T))
            throw new InvalidCastException(
                $"SqlClient-style strict reader: Get{typeof(T).Name} called for storage {_storage[ordinal].Name} at ordinal {ordinal}.");

        TypedReadCalls++;
        return (T)_values[ordinal]!;
    }
}
