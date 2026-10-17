namespace NextORM.Benchmark;

/// <summary>
/// Observable, allocation-free row consumer shared by the tier-1 comparison classes. Every measured
/// arm folds its rows into the same sink shape (a row count, an order-independent checksum and an
/// optional byte count) so BenchmarkDotNet cannot dead-code-eliminate the query and so the arms can
/// be compared on identical work. The sink is reset outside the measured region and read once after
/// each invocation; it never retains references to the rows.
/// </summary>
internal sealed class BenchmarkRowSink
{
    /// <summary>Rows observed since the last <see cref="Reset"/>.</summary>
    public long Rows { get; private set; }

    /// <summary>Order-sensitive checksum of the folded column values, to prove the same rows were read.</summary>
    public long Checksum { get; private set; }

    /// <summary>Bytes written to an optional output arm (JSON/CSV), otherwise zero.</summary>
    public long Bytes { get; private set; }

    /// <summary>Clears the counters so the next measured body starts from a known state.</summary>
    public void Reset()
    {
        Rows = 0;
        Checksum = 0;
        Bytes = 0;
    }

    /// <summary>Folds one scalar column value into the checksum and advances the row count.</summary>
    public void Add(long value)
    {
        Rows++;
        Checksum = unchecked((Checksum * 31) + value);
    }

    /// <summary>Records a nullable scalar column, folding a stable sentinel for <see langword="null"/>.</summary>
    public void Add(long? value) => Add(value ?? long.MinValue);

    /// <summary>Records a string column by its length, so wide values do not allocate.</summary>
    public void Add(string? value) => Add(value?.Length ?? -1);

    /// <summary>Adds a byte count produced by an output arm without retaining the payload.</summary>
    public void AddBytes(long bytes) => Bytes += bytes;

    /// <summary>A deterministic token describing how many rows were consumed, for setup assertions.</summary>
    public override string ToString() => $"rows={Rows}, checksum={Checksum}, bytes={Bytes}";
}
