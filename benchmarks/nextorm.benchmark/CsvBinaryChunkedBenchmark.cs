using System.Data;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// D167 (#167) paired benchmark for the chunked CSV byte[] path. The legacy arm runs the preserved
/// buffered branch the same way the compiled column plan did before the change
/// (<see cref="CsvValueFormatter.WriteBytes"/> over a whole field read with
/// <c>IDataRecord.GetFieldValue&lt;byte[]&gt;</c>); the chunked arm drives the new
/// <see cref="CsvBinaryFieldWriter"/> bounded <c>GetBytes</c> path. Both arms emit the same CSV
/// bytes for the same deterministic payload and write to a counting sink that does not retain the
/// result.
/// </summary>
/// <remarks>
/// The payload is never materialised in a setup field: <see cref="ChunkedBinaryProbe"/> generates
/// every byte from its offset on demand, so the legacy arm pays the whole-BLOB allocation inside the
/// measured region (as the buffered production path does) while the chunked arm only ever touches its
/// two fixed pooled buffers. The row buffer is warmed once per case and reused, matching how the CSV
/// terminal reuses one row buffer across the rows of a call; the CPU/allocation difference measured
/// here is therefore the path difference, not buffer setup.
/// </remarks>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("csv-binary-chunked")]
public class CsvBinaryChunkedBenchmark
{
    private const int Small = 64;
    private const int OneMiB = 1024 * 1024;
    private const int SixteenMiB = 16 * 1024 * 1024;

    /// <summary>The single binary payload size for the case: 64 B, 1 MiB and 16 MiB.</summary>
    [Params(Small, OneMiB, SixteenMiB)]
    public int PayloadBytes { get; set; }

    private CsvDialect _dialect = null!;
    private CsvFieldPolicy _policy = null!;
    private ChunkedBinaryProbe _probe = null!;
    private CsvRowBuffer _buffer = null!;
    private CountingSink _sink = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dialect = new CsvDialect(new CsvStreamOptions().Delimiter);
        _policy = CsvFieldPolicy.Default;
        _probe = new ChunkedBinaryProbe(PayloadBytes);
        _buffer = new CsvRowBuffer(_dialect, _policy);
        _sink = new CountingSink();

        if (!CsvBinaryFieldWriter.IsEligible(_dialect, _policy))
            throw new InvalidOperationException("The benchmark defaults must admit the bounded binary path.");

        VerifyPairedOutput();

        // Warm the reused row buffer (and the legacy path) outside the measured region.
        Legacy_Buffered();
        Chunked();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
        _sink.Dispose();
    }

    /// <summary>The preserved buffered branch: read the whole field, then Base64 it through the row buffer.</summary>
    [Benchmark(Baseline = true)]
    public long Legacy_Buffered()
    {
        _sink.Reset();
        _buffer.Reset();
        CsvValueFormatter.WriteBytes(_buffer, _probe.GetFieldValue<byte[]>(0));
        _buffer.WriteRowTerminator();
        _sink.Write(_buffer.Written);
        return _sink.Total;
    }

    /// <summary>The new bounded branch: Base64 the field through GetBytes in 12 KiB input chunks, straight to the sink.</summary>
    [Benchmark]
    public long Chunked()
    {
        _sink.Reset();
        _buffer.Reset();
        CsvBinaryFieldWriter.Write(_probe, 0, _buffer, _sink, CancellationToken.None);
        _buffer.WriteRowTerminator();
        _sink.Write(_buffer.Written);
        return _sink.Total;
    }

    /// <summary>
    /// Proves, outside the measured region, that the two arms produce byte-identical CSV and that the
    /// 64 B case matches the <see cref="Convert.ToBase64String(byte[])"/> oracle.
    /// </summary>
    private void VerifyPairedOutput()
    {
        byte[] legacy;
        using (var legacyBuffer = new CsvRowBuffer(_dialect, _policy))
        using (var destination = new MemoryStream())
        {
            CsvValueFormatter.WriteBytes(legacyBuffer, _probe.GetFieldValue<byte[]>(0));
            legacyBuffer.WriteRowTerminator();
            destination.Write(legacyBuffer.Written);
            legacy = destination.ToArray();
        }

        byte[] chunked;
        using (var chunkBuffer = new CsvRowBuffer(_dialect, _policy))
        using (var destination = new MemoryStream())
        {
            CsvBinaryFieldWriter.Write(_probe, 0, chunkBuffer, destination, CancellationToken.None);
            chunkBuffer.WriteRowTerminator();
            destination.Write(chunkBuffer.Written);
            chunked = destination.ToArray();
        }

        if (!legacy.AsSpan().SequenceEqual(chunked))
            throw new InvalidOperationException($"Chunked CSV differs from the legacy buffered path for payload {PayloadBytes}.");

        if (PayloadBytes == Small)
        {
            var expected = Encoding.UTF8.GetBytes(Convert.ToBase64String(_probe.GetFieldValue<byte[]>(0)) + "\r\n");
            if (!expected.AsSpan().SequenceEqual(chunked))
                throw new InvalidOperationException("The 64 B CSV does not match the Base64 oracle.");
        }
    }

    /// <summary>
    /// A deterministic <see cref="IDataRecord"/> for one binary column. It generates every byte from
    /// its logical offset instead of storing the payload, so no whole BLOB exists between reads; only
    /// the legacy arm's explicit <see cref="GetFieldValue{T}"/> materialises one.
    /// </summary>
    private sealed class ChunkedBinaryProbe : IDataRecord
    {
        private readonly int _length;

        public ChunkedBinaryProbe(int length) => _length = length;

        private static byte ByteAt(long offset) => (byte)((offset * 31 + 7) & 0xFF);

        public int FieldCount => 1;

        public object this[int i] => throw new NotSupportedException();

        public object this[string name] => throw new NotSupportedException();

        public string GetName(int i) => "Payload";

        public string GetDataTypeName(int i) => "blob";

        public Type GetFieldType(int i) => typeof(byte[]);

        public object GetValue(int i) => throw new NotSupportedException();

        public int GetValues(object[] values) => throw new NotSupportedException();

        public bool IsDBNull(int i) => false;

        public T GetFieldValue<T>(int i)
        {
            if (typeof(T) != typeof(byte[]))
                throw new NotSupportedException($"Only byte[] is supported, not {typeof(T)}.");

            var value = new byte[_length];
            for (var offset = 0; offset < value.Length; offset++)
                value[offset] = ByteAt(offset);

            return (T)(object)value;
        }

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
        {
            if (buffer is null)
                throw new NotSupportedException("A null GetBytes buffer (length probe) is not used by the bounded path.");

            if (fieldOffset >= _length)
                return 0;

            var count = (int)Math.Min(length, _length - fieldOffset);
            for (var j = 0; j < count; j++)
                buffer[bufferoffset + j] = ByteAt(fieldOffset + j);

            return count;
        }

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public float GetFloat(int i) => throw new NotSupportedException();

        public double GetDouble(int i) => throw new NotSupportedException();

        public string GetString(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => throw new NotSupportedException();

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public int GetOrdinal(string name) => 0;
    }

    /// <summary>A write-only sink that counts the bytes it is handed and retains none of them.</summary>
    private sealed class CountingSink : Stream
    {
        public long Total { get; private set; }

        public void Reset() => Total = 0;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Total;

        public override long Position
        {
            get => Total;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Total += count;

        public override void Write(ReadOnlySpan<byte> buffer) => Total += buffer.Length;

        public override void WriteByte(byte value) => Total++;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Total += buffer.Length;
            return default;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
