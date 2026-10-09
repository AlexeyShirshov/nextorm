namespace NextORM.Benchmark;

/// <summary>
/// Setup-time semantic probes shared by the tier-1 comparison classes. Every arm's workload is
/// executed once in <c>[GlobalSetup]</c> and its row count/checksum compared here, outside the timed
/// region, so a benchmark body that silently reads different rows cannot be reported as equivalent.
/// </summary>
internal static class BenchmarkComparisonValidation
{
    /// <summary>Throws <see cref="InvalidOperationException"/> when <paramref name="condition"/> is false.</summary>
    public static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Throws when the observed count differs from the expected count.</summary>
    public static void EnsureCount(long expected, long actual, string context)
        => Ensure(expected == actual, $"{context}: expected {expected} rows, observed {actual}.");

    /// <summary>Throws when the two checksums differ.</summary>
    public static void EnsureChecksum(long expected, long actual, string context)
        => Ensure(expected == actual, $"{context}: expected checksum {expected}, observed {actual}.");

    /// <summary>Computes the order-sensitive checksum the shared <see cref="BenchmarkRowSink"/> uses.</summary>
    public static long Checksum<T>(IEnumerable<T> rows, Func<T, long> selector)
    {
        var checksum = 0L;
        foreach (var row in rows)
            checksum = unchecked((checksum * 31) + selector(row));
        return checksum;
    }

    /// <summary>Counts the rows through the same selector used by the checksum (materializes once).</summary>
    public static (int Count, long Checksum) Measure<T>(IEnumerable<T> rows, Func<T, long> selector)
    {
        var count = 0;
        var checksum = 0L;
        foreach (var row in rows)
        {
            count++;
            checksum = unchecked((checksum * 31) + selector(row));
        }
        return (count, checksum);
    }

    /// <summary>Writes a one-line observation to the benchmark log (BDN captures Console output).</summary>
    public static void Report(string context, long count, long checksum, string? extra = null)
        => Console.WriteLine($"[D188] {context}: rows={count}, checksum={checksum}{(extra is null ? string.Empty : ", " + extra)}");
}
