using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// A growable, pooled UTF-8 byte buffer that accumulates one CSV row (or the header row) before it is
/// written to the destination stream. Capacity is rented from <see cref="ArrayPool{T}"/> and reused
/// across every row of a call; it grows in place only when a single row exceeds the current capacity.
/// The buffer is fully written out and reset per row, so a partial row is never left on the stream.
/// </summary>
internal sealed class CsvRowBuffer : IDisposable
{
    private const int InitialCapacity = 4 * 1024;

    private byte[] _buffer;
    private int _length;

    public CsvRowBuffer(CsvDialect dialect)
        : this(dialect, CsvFieldPolicy.Default)
    {
    }

    public CsvRowBuffer(CsvDialect dialect, CsvFieldPolicy policy)
    {
        Dialect = dialect;
        Policy = policy;
        _buffer = ArrayPool<byte>.Shared.Rent(InitialCapacity);
    }

    /// <summary>The dialect used for escaping and for writing the field separator.</summary>
    public CsvDialect Dialect { get; }

    /// <summary>The resolved NULL marker, Excel guard and per-value transform of this call.</summary>
    public CsvFieldPolicy Policy { get; }

    /// <summary>The bytes accumulated for the current row.</summary>
    public ReadOnlySpan<byte> Written => _buffer.AsSpan(0, _length);

    /// <summary>The bytes accumulated for the current row, for an asynchronous write.</summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _length);

    /// <summary>Discards the accumulated bytes, keeping the rented capacity for the next row.</summary>
    public void Reset() => _length = 0;

    /// <summary>Appends a single byte.</summary>
    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_length++] = value;
    }

    /// <summary>Appends the dialect's UTF-8 encoded field separator.</summary>
    public void WriteDelimiter() => Write(Dialect.DelimiterUtf8);

    /// <summary>Appends an ASCII/UTF-8 byte sequence.</summary>
    public void Write(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        EnsureCapacity(value.Length);
        value.CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;
    }

    /// <summary>Appends a UTF-8 encoded character sequence.</summary>
    public void WriteUtf8(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return;

        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount == 0)
            return;

        EnsureCapacity(byteCount);
        _length += Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_length));
    }

    /// <summary>Appends the RFC 4180 representation of a string field (quoted only when required).</summary>
    public void WriteStringField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (!Dialect.NeedsQuoting(value.AsSpan()))
        {
            WriteUtf8(value.AsSpan());
            return;
        }

        WriteByte((byte)'"');

        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                WriteUtf8(value.AsSpan(start, i - start));
                WriteByte((byte)'"');
                WriteByte((byte)'"');
                start = i + 1;
            }
        }

        WriteUtf8(value.AsSpan(start));
        WriteByte((byte)'"');
    }

    /// <summary>Appends the configured NULL marker as a field.</summary>
    public void WriteNullField() => Write(Policy.NullMarkerUtf8);

    /// <summary>
    /// Appends a string field under the call's policy: <see langword="null"/> becomes the NULL marker, an
    /// empty string stays an empty field, a non-NULL value equal to the NULL marker is quoted so it cannot be
    /// read back as NULL, and the Excel guard runs before RFC 4180 escaping.
    /// </summary>
    public void WriteField(string? value)
    {
        if (value is null)
        {
            WriteNullField();
            return;
        }

        if (value.Length == 0)
            return;

        if (string.Equals(value, Policy.NullMarker, StringComparison.Ordinal))
        {
            WriteQuoted(Encoding.UTF8.GetBytes(value));
            return;
        }

        if (Policy.ExcelMode && IsFormulaStart(value[0]))
        {
            // The apostrophe is part of the field value, so it must sit inside the RFC 4180 quotes
            // (not before them); prepending before escaping keeps the guard and the quoting consistent.
            var guarded = "'" + value;
            if (string.Equals(guarded, Policy.NullMarker, StringComparison.Ordinal))
            {
                WriteQuoted(Encoding.UTF8.GetBytes(guarded));
                return;
            }

            WriteStringField(guarded);
            return;
        }

        WriteStringField(value);
    }

    /// <summary>Appends a string value, routing it through the per-value transform when one is configured.</summary>
    public void WriteStringFieldWithPolicy(string? value)
    {
        if (Policy.ValueTransform is { } transform && value is not null)
        {
            WriteField(transform(value));
            return;
        }

        WriteField(value);
    }

    /// <summary>Appends a byte-array field under the call's policy (NULL marker, transform, then Base64).</summary>
    public void WriteBytesField(byte[]? value)
    {
        if (value is null)
        {
            WriteNullField();
            return;
        }

        if (value.Length == 0)
            return;

        if (Policy.ValueTransform is { } transform)
        {
            WriteField(transform(value));
            return;
        }

        WriteBase64Field(value);
    }

    /// <summary>
    /// Appends an already-formatted field under the call's policy: a value equal to the NULL marker is quoted,
    /// the Excel guard prefixes a formula starter, and the result is escaped with RFC 4180 rules.
    /// </summary>
    public void WriteEscapedField(ReadOnlySpan<byte> field)
    {
        if (field.IsEmpty)
            return;

        if (field.SequenceEqual(Policy.NullMarkerUtf8))
        {
            WriteQuoted(field);
            return;
        }

        if (Policy.ExcelMode && IsFormulaStart(field[0]))
        {
            WriteGuardedField(field);
            return;
        }

        if (!Dialect.NeedsQuoting(field))
        {
            Write(field);
            return;
        }

        WriteQuoted(field);
    }

    /// <summary>
    /// Appends <paramref name="field"/> prefixed with the Excel guard apostrophe. When the field needs
    /// RFC 4180 quoting, the apostrophe goes inside the quotes so the value stays a single field.
    /// </summary>
    private void WriteGuardedField(ReadOnlySpan<byte> field)
    {
        // The guard apostrophe can itself complete the NULL marker (e.g. marker "'=x" for a field "=x"),
        // so the guarded result must be force-quoted to stay distinguishable from NULL. This mirrors the
        // string path in WriteField; without it a present numeric/Base64 value reads back as NULL.
        if (!Dialect.NeedsQuoting(field) && !GuardedEqualsNullMarker(field))
        {
            WriteByte((byte)'\'');
            Write(field);
            return;
        }

        WriteByte((byte)'"');
        WriteByte((byte)'\'');
        foreach (var item in field)
        {
            if (item == (byte)'"')
                WriteByte((byte)'"');

            WriteByte(item);
        }

        WriteByte((byte)'"');
    }

    /// <summary>Whether prefixing the Excel guard makes <paramref name="field"/> equal the NULL marker.</summary>
    private bool GuardedEqualsNullMarker(ReadOnlySpan<byte> field)
    {
        var marker = Policy.NullMarkerUtf8;
        return marker.Length == field.Length + 1
            && marker[0] == (byte)'\''
            && field.SequenceEqual(marker.AsSpan(1));
    }

    private static bool IsFormulaStart(byte value) => value is (byte)'=' or (byte)'+' or (byte)'-' or (byte)'@';

    private static bool IsFormulaStart(char value) => value is '=' or '+' or '-' or '@';

    /// <summary>
    /// Appends the Base64 representation of a byte array field under the call's field policy: the encoded
    /// text is routed through the same marker-collision quoting, Excel guard and RFC 4180 escaping as a
    /// string field, so a Base64 payload can never be read back as NULL and a leading <c>+</c> is guarded.
    /// </summary>
    public void WriteBase64Field(byte[]? value)
    {
        if (value is null || value.Length == 0)
            return;

        // Base64 needs the complete encoded field before the policy can decide whether it must be quoted
        // (delimiter/CR/LF) or guarded (a leading '+'), so encode into a pooled scratch buffer.
        var rented = ArrayPool<byte>.Shared.Rent(Base64.GetMaxEncodedToUtf8Length(value.Length));
        try
        {
            if (Base64.EncodeToUtf8(value, rented, out _, out var written) != OperationStatus.Done)
                throw new InvalidOperationException("The Base64 CSV field did not fit its destination buffer.");

            WriteEscapedField(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Appends the raw bytes of a field wrapped in double quotes, doubling every inner quote.</summary>
    public void WriteQuoted(ReadOnlySpan<byte> value)
    {
        WriteByte((byte)'"');
        foreach (var item in value)
        {
            if (item == (byte)'"')
                WriteByte((byte)'"');

            WriteByte(item);
        }

        WriteByte((byte)'"');
    }

    /// <summary>Appends the CRLF row terminator.</summary>
    public void WriteRowTerminator()
    {
        WriteByte((byte)'\r');
        WriteByte((byte)'\n');
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var buffer = _buffer;
        _buffer = [];
        if (buffer.Length > 0)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    private void EnsureCapacity(int additional)
    {
        var required = _length + additional;
        if (required <= _buffer.Length)
            return;

        var capacity = Math.Max(required, _buffer.Length * 2);
        var grown = ArrayPool<byte>.Shared.Rent(capacity);
        _buffer.AsSpan(0, _length).CopyTo(grown);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = grown;
    }
}
