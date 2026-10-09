using System.Buffers;
using System.Buffers.Text;
using System.Data;

namespace NextORM.Core;

/// <summary>
/// Streams one direct-storage <c>byte[]</c> CSV field from a sequential reader to the destination in
/// bounded chunks, Base64-encoding only complete three-byte groups and flushing the padded tail once at
/// EOF. The whole field is never materialised: the input and output buffers are fixed-size pooled arrays
/// returned in a <c>finally</c>. The path is admitted only for a field whose framed output is provably
/// identical to the buffered <see cref="CsvRowBuffer.WriteBase64Field(byte[])"/> path (see
/// <see cref="IsEligible"/>), and it fails closed: a provider that throws from <c>GetBytes</c> aborts the
/// export instead of falling back to a whole-array getter.
/// </summary>
internal static class CsvBinaryFieldWriter
{
    /// <summary>
    /// The logical input capacity, 12 KiB. It is a multiple of three so a full buffer encodes with no
    /// padding, and it is the request size passed to <see cref="IDataRecord.GetBytes"/>; the rented array
    /// may be larger, which is never requested.
    /// </summary>
    internal const int BinaryCapacity = 12 * 1024;

    private static readonly byte[] GuardApostrophe = [(byte)'\''];

    /// <summary>
    /// True when a direct <c>byte[]</c> field may be streamed through the bounded path: no per-value
    /// transform, the delimiter never occurs in the Base64 alphabet (so the encoded field never needs
    /// RFC 4180 quoting), and the NULL marker cannot collide with a Base64 payload (plain or Excel
    /// guarded), so the framing is knowable before the field is read.
    /// </summary>
    internal static bool IsEligible(CsvDialect dialect, CsvFieldPolicy policy)
    {
        if (policy.ValueTransform is not null)
            return false;

        foreach (var value in dialect.DelimiterUtf8)
        {
            if (IsBase64Byte(value))
                return false;
        }

        // A Base64 payload consists solely of alphabet bytes, so a marker holding a non-alphabet byte can
        // never equal one; the Excel guard additionally prefixes an apostrophe, so a marker that itself
        // starts with one is excluded too.
        if (policy.ExcelMode && policy.NullMarkerUtf8 is [(byte)'\'', ..])
            return false;

        return !IsAllBase64(policy.NullMarkerUtf8);
    }

    /// <summary>Writes one chunked binary field synchronously; a SQL NULL is written to <paramref name="buffer"/> as the NULL marker.</summary>
    /// <param name="record">The open reader positioned on the current row.</param>
    /// <param name="ordinal">The reader ordinal of the direct binary column.</param>
    /// <param name="buffer">The row buffer, used for its field policy (NULL marker and Excel guard).</param>
    /// <param name="destination">The stream that receives the Base64; it stays open.</param>
    /// <param name="cancellationToken">Checked before the first binary read and between reads; an in-flight synchronous provider call is not interrupted.</param>
    public static void Write(IDataRecord record, int ordinal, CsvRowBuffer buffer, Stream destination, CancellationToken cancellationToken)
    {
        ValidateOrdinal(record, ordinal);
        if (record.IsDBNull(ordinal))
        {
            buffer.WriteNullField();
            return;
        }

        // The sync path cannot interrupt an in-flight provider GetBytes call, but it must still observe
        // the token before the first read and between reads (see the async twin for the full granularity).
        cancellationToken.ThrowIfCancellationRequested();

        using var reader = new CsvBinaryChunkReader(record, ordinal);
        if (!reader.TryReadChunk(out var written))
        {
            // A non-null empty field emits nothing, exactly like the buffered path.
            return;
        }

        if (buffer.Policy.ExcelMode && reader.StartsWithPlus)
            destination.Write(GuardApostrophe);

        destination.Write(reader.OutputSpan(written));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.TryReadChunk(out written))
                break;

            destination.Write(reader.OutputSpan(written));
        }
    }

    /// <summary>Writes one chunked binary field asynchronously, awaiting the destination between chunks.</summary>
    /// <param name="record">The open reader positioned on the current row.</param>
    /// <param name="ordinal">The reader ordinal of the direct binary column.</param>
    /// <param name="buffer">The row buffer, used for its field policy (NULL marker and Excel guard).</param>
    /// <param name="destination">The stream that receives the Base64; it stays open.</param>
    /// <param name="cancellationToken">Checked between binary reads and used by the destination write.</param>
    /// <returns>A task that completes when the field has been written.</returns>
    public static async Task WriteAsync(IDataRecord record, int ordinal, CsvRowBuffer buffer, Stream destination, CancellationToken cancellationToken)
    {
        ValidateOrdinal(record, ordinal);
        if (record.IsDBNull(ordinal))
        {
            buffer.WriteNullField();
            return;
        }

        using var reader = new CsvBinaryChunkReader(record, ordinal);
        if (!reader.TryReadChunk(out var written))
        {
            return;
        }

        if (buffer.Policy.ExcelMode && reader.StartsWithPlus)
            await destination.WriteAsync(GuardApostrophe, cancellationToken).ConfigureAwait(false);

        await destination.WriteAsync(reader.OutputMemory(written), cancellationToken).ConfigureAwait(false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.TryReadChunk(out written))
                break;

            await destination.WriteAsync(reader.OutputMemory(written), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fails closed when the bounded path is asked about an ordinal the reader does not have: the
    /// projection admission should make this unreachable, but an out-of-range ordinal must surface as a
    /// managed error rather than reaching the provider.
    /// </summary>
    private static void ValidateOrdinal(IDataRecord record, int ordinal)
    {
        if (ordinal < 0 || ordinal >= record.FieldCount)
            throw new NotSupportedException(
                $"The CSV bounded binary path was asked to read ordinal {ordinal}, which is outside the reader's field count {record.FieldCount}.");
    }

    private static bool IsAllBase64(byte[] value)
    {
        foreach (var b in value)
        {
            if (!IsBase64Byte(b))
                return false;
        }

        return value.Length > 0;
    }

    private static bool IsBase64Byte(byte value)
        => value is (>= (byte)'A' and <= (byte)'Z')
            or (>= (byte)'a' and <= (byte)'z')
            or (>= (byte)'0' and <= (byte)'9')
            or (byte)'+' or (byte)'/' or (byte)'=';

    /// <summary>
    /// Reads one binary field in bounded chunks. A short <see cref="IDataRecord.GetBytes"/> read is not
    /// EOF; only a returned count of zero ends the field, after which a 1-2 byte tail is padded once.
    /// </summary>
    private sealed class CsvBinaryChunkReader : IDisposable
    {
        private readonly IDataRecord _record;
        private readonly int _ordinal;
        private readonly byte[] _input;
        private readonly byte[] _output;
        private long _offset;
        private int _carry;
        private bool _eof;
        private byte _firstByte;
        private bool _firstByteLatched;

        public CsvBinaryChunkReader(IDataRecord record, int ordinal)
        {
            _record = record;
            _ordinal = ordinal;
            var input = ArrayPool<byte>.Shared.Rent(BinaryCapacity);
            try
            {
                _output = ArrayPool<byte>.Shared.Rent(Base64.GetMaxEncodedToUtf8Length(BinaryCapacity));
            }
            catch
            {
                // The two rents are not atomic: if the output rent throws, return the already-rented
                // input instead of leaking it. The buffer was rented but never written, so it needs no
                // clearing; see Dispose for the raw-blob trade-off.
                ArrayPool<byte>.Shared.Return(input, clearArray: false);
                throw;
            }

            _input = input;
        }

        /// <summary>
        /// Whether the first byte of the field encodes to <c>+</c> (the only Base64 Excel-guard starter).
        /// Latched from the first byte read into the input buffer, before the carry logic can shift a
        /// trailing 1-2 byte remainder over <c>_input[0]</c>.
        /// </summary>
        public bool StartsWithPlus => (_firstByte >> 2) == 62;

        public ReadOnlySpan<byte> OutputSpan(int length) => _output.AsSpan(0, length);

        public ReadOnlyMemory<byte> OutputMemory(int length) => _output.AsMemory(0, length);

        /// <summary>Produces the next bounded Base64 chunk; returns <see langword="false"/> once the field is exhausted.</summary>
        /// <param name="written">The number of bytes written to the output buffer; zero at end of field.</param>
        /// <returns><see langword="true"/> when a chunk was produced, <see langword="false"/> at end of field.</returns>
        public bool TryReadChunk(out int written)
        {
            written = 0;
            while (true)
            {
                if (_eof)
                    return false;

                // Fill behind the carry bytes: positions [0, _carry) hold the previous 1-2 byte tail.
                var requested = BinaryCapacity - _carry;
                var returned = _record.GetBytes(_ordinal, _offset, _input, _carry, requested);
                if (returned < 0 || returned > requested)
                    throw new InvalidOperationException(
                        $"The provider returned {returned} bytes from IDataRecord.GetBytes(ordinal: {_ordinal}, offset: {_offset}) for a request of {requested}; a bounded binary field cannot be streamed from an inconsistent read.");

                var read = (int)returned;
                if (read == 0)
                {
                    _eof = true;
                    if (_carry == 0)
                        return false;

                    // The final tail is padded once; this is the only place a trailing '=' can appear.
                    Encode(_carry, out written);
                    _carry = 0;
                    return true;
                }

                _offset += read;

                // Latch the first field byte at fill time, before any group encode can move a trailing
                // 1-2 byte remainder onto _input[0] (which would make the Excel guard read the wrong byte).
                if (!_firstByteLatched)
                {
                    _firstByte = _input[0];
                    _firstByteLatched = true;
                }

                var total = _carry + read;
                var complete = total - (total % 3);
                if (complete == 0)
                {
                    // Fewer than three bytes so far (a short read, not EOF): hold them and read again.
                    _carry = total;
                    continue;
                }

                Encode(complete, out written);
                var leftover = total - complete;
                if (leftover > 0)
                    Buffer.BlockCopy(_input, complete, _input, 0, leftover);
                _carry = leftover;
                return true;
            }
        }

        private void Encode(int length, out int written)
        {
            if (Base64.EncodeToUtf8(_input.AsSpan(0, length), _output, out _, out written) != OperationStatus.Done)
                throw new InvalidOperationException("The Base64 CSV field did not fit its destination buffer.");
        }

        public void Dispose()
        {
            // Return both buffers without clearing. The input holds raw blob bytes, but it is
            // process-local pooled memory that the next field's GetBytes immediately overwrites before
            // it can be read (the writer never exposes a pooled buffer between operations), and clearing
            // a fixed 12 KiB on every operation dominated the 64 B payload (+134% vs the buffered path
            // in D4d). Clearing the raw blob is therefore superseded by the plan's A3 perf acceptance;
            // the output holds only the already-public Base64 payload.
            ArrayPool<byte>.Shared.Return(_input, clearArray: false);
            ArrayPool<byte>.Shared.Return(_output);
        }
    }
}
