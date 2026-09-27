using System.Data;
using Microsoft.Data.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Capability probe (PDCA cycle 2, issue #27): on a real Microsoft.Data.Sqlite 10.0.12 connection,
/// does <see cref="System.Data.Common.DbDataReader.GetStream(int)"/> under
/// <see cref="System.Data.CommandBehavior.SequentialAccess"/> return a memory-bounded
/// <c>SqliteBlob</c> when the query also projects <c>rowid</c>, versus a buffered
/// <see cref="MemoryStream"/> when it does not?
/// <para>
/// The driver is used directly (<see cref="SqliteConnection"/>/<see cref="SqliteCommand"/>), so the
/// nextorm LOB terminals and their <c>FieldCount != 1</c> guard are bypassed (the guard is left
/// untouched). A normal rowid table <c>t(id INTEGER PRIMARY KEY, payload BLOB)</c> is seeded with
/// 1 MiB, 8 MiB and 32 MiB rows; each read uses a fixed 64 KiB buffer and reports the best-of-3
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> delta plus the peak live-managed delta
/// (<see cref="GC.GetTotalMemory(bool)"/> sampled every buffer iteration, relative to the
/// pre-read baseline). A memory-bounded read has <c>alloc(size) / alloc(1 MiB) ≈ 1</c> and a
/// flat peak; a buffered read shows <c>≈ size / 1 MiB</c> and a peak that grows with the size.
/// Each row is addressed by its explicit <c>id</c>, so the size-to-row mapping never depends on
/// an unordered <c>SELECT</c>'s implicit order.
/// </para>
/// <para>
/// It also documents the fail-closed behavior for a view and a <c>WITHOUT ROWID</c> table, and
/// records the <c>FieldCount</c> of both projections (a trailing <c>rowid</c> makes it 2, which the
/// existing nextorm guard would reject).
/// </para>
/// <para>
/// Out-of-band probe: it only runs when <c>NEXTORM_LOB_SQLITE_PROBE=1</c> is set, so the normal
/// suite does not pay for the multi-megabyte seeds and reads. No containers are required.
/// </para>
/// </summary>
public sealed class SqliteRowIdLobProbeTests
{
    private const int MiB = 1024 * 1024;
    private const int ReadBufferBytes = 64 * 1024;
    private const int Samples = 3;

    private static readonly int[] Sizes = [MiB, 8 * MiB, 32 * MiB];

    // Deterministic byte pattern with period 256: byte(i) = (byte)((i * 131 + 7) & 0xFF).
    private static readonly byte[] Pattern = BuildPattern();

    private static string OutputPath =>
        Environment.GetEnvironmentVariable("NEXTORM_LOB_SQLITE_PROBE_LOG")
        ?? "/tmp/lob-sqlite-rowid-probe.log";

    [Fact]
    public void Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("NEXTORM_LOB_SQLITE_PROBE") == "1",
            "Set NEXTORM_LOB_SQLITE_PROBE=1 to run the SQLite rowid LOB capability probe.");

        var databasePath = Path.Combine(Path.GetTempPath(), $"nextorm-lob-rowid-{Guid.NewGuid():N}.db");
        var lines = new List<string>
        {
            $"# SQLite rowid LOB capability probe; driver Microsoft.Data.Sqlite 10.0.12; runtime {Environment.Version}",
            $"# database {databasePath}",
            "# shape | query | size | stream type | bytes | allocated (best of 3) | peak live-delta | ratio vs 1 MiB | correct",
        };

        try
        {
            // Pooling=false: a pooled connection would keep the OS handle open after Dispose and
            // (on Windows) silently defeat the temp-file delete below.
            using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            connection.Open();

            Seed(connection);

            foreach (var shape in new[] { "baseline", "rowid" })
            {
                // The row is addressed by its explicit id (seeded as size index + 1) so the
                // size -> row mapping never depends on an unordered SELECT's implicit order.
                var projection = shape == "baseline"
                    ? "SELECT payload FROM t WHERE id = $id"
                    : "SELECT payload, rowid FROM t WHERE id = $id";

                var results = new ProbeResult[Sizes.Length];
                for (var sizeIndex = 0; sizeIndex < Sizes.Length; sizeIndex++)
                    results[sizeIndex] = Measure(connection, projection, sizeIndex + 1, Sizes[sizeIndex]);

                var reference = results[0].Allocated;
                for (var i = 0; i < Sizes.Length; i++)
                {
                    var result = results[i];
                    var ratio = i == 0
                        ? "1.00 (ref)"
                        : reference > 0 && result.Allocated > 0
                            ? ((double)result.Allocated / reference).ToString("F2")
                            : "n/a";

                    lines.Add(
                        $"[{shape}] \"{projection}\" | {Sizes[i] / MiB} MiB | {result.StreamType} | {result.Bytes} | " +
                        $"{result.Allocated} | {result.PeakDelta} | {ratio} | {(result.Correct ? "ok" : "FAIL")}" +
                        (result.Error is null ? string.Empty : $" error={result.Error}"));
                }

                lines.Add($"[{shape}] FieldCount={results[0].FieldCount}");
            }

            // Fail-closed documentation: a view and a WITHOUT ROWID table.
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE VIEW v AS SELECT id, payload FROM t;";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "CREATE TABLE t2(id INTEGER PRIMARY KEY, payload BLOB) WITHOUT ROWID;" +
                    "INSERT INTO t2 (id, payload) VALUES (1, x'01020304');";
                command.ExecuteNonQuery();
            }

            lines.Add($"[view] \"SELECT payload, rowid FROM v\" -> {ProbeFailure(connection, "SELECT payload, rowid FROM v")}");
            lines.Add($"[without-rowid] \"SELECT payload, rowid FROM t2\" -> {ProbeFailure(connection, "SELECT payload, rowid FROM t2")}");
        }
        finally
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (Exception exception)
            {
                // Do not swallow: record why the temp database could not be removed.
                lines.Add($"[cleanup] File.Delete failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        File.AppendAllLines(OutputPath, lines);
    }

    private static ProbeResult Measure(
        SqliteConnection connection, string projection, int id, int expectedSize)
    {
        var buffer = new byte[ReadBufferBytes];

        ProbeResult ReadOnce()
        {
            using var command = connection.CreateCommand();
            command.CommandText = projection;
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);

            if (!reader.Read())
                throw new InvalidOperationException($"id {id} was not returned by '{projection}'");

            var fieldCount = reader.FieldCount;

            // Baseline before GetStream: it captures the (potentially whole-value) allocation the
            // buffered path performs inside GetStream.
            var baseLine = GC.GetTotalMemory(forceFullCollection: false);
            using var stream = reader.GetStream(0);
            var streamType = stream.GetType().Name;

            var peak = Math.Max(0, GC.GetTotalMemory(forceFullCollection: false) - baseLine);
            long length = 0;
            var correct = true;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] != Pattern[(int)((length + i) & 0xFF)])
                        correct = false;
                }

                length += read;

                var delta = GC.GetTotalMemory(forceFullCollection: false) - baseLine;
                if (delta > peak)
                    peak = delta;
            }

            if (length != expectedSize)
                correct = false;

            return new ProbeResult(streamType, fieldCount, length, 0, peak, correct, null);
        }

        try
        {
            // Warm up JIT and any per-connection state before measuring.
            ReadOnce();
            ReadOnce();

            var best = long.MaxValue;
            var peak = 0L;
            ProbeResult last = default;
            for (var i = 0; i < Samples; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                last = ReadOnce();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated < best)
                    best = allocated;
                if (last.PeakDelta > peak)
                    peak = last.PeakDelta;
            }

            return last with { Allocated = best, PeakDelta = peak };
        }
        catch (Exception exception)
        {
            return new ProbeResult("<error>", -1, -1, -1, -1, false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string ProbeFailure(SqliteConnection connection, string commandText)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            var fieldCount = reader.FieldCount;

            if (!reader.Read())
                return $"no rows (FieldCount={fieldCount})";

            using var stream = reader.GetStream(0);
            var streamType = stream.GetType().Name;
            var buffer = new byte[ReadBufferBytes];
            long total = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                total += read;

            return $"no exception; stream={streamType} FieldCount={fieldCount} bytes={total}";
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().FullName}: {exception.Message}";
        }
    }

    private static void Seed(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "DROP TABLE IF EXISTS t;" +
                "CREATE TABLE t(id INTEGER PRIMARY KEY, payload BLOB NOT NULL);";
            command.ExecuteNonQuery();
        }

        // One transient deterministic buffer per size; correctness beats avoiding the single
        // largest allocation (SQLite parameters must be contiguous). Ids are sizeIndex + 1 so the
        // probe reads each size back by an explicit id.
        for (var i = 0; i < Sizes.Length; i++)
        {
            var data = new byte[Sizes[i]];
            for (var j = 0; j < data.Length; j++)
                data[j] = Pattern[j & 0xFF];

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO t (id, payload) VALUES ($id, $payload);";
            command.Parameters.AddWithValue("$id", i + 1);
            command.Parameters.Add("$payload", SqliteType.Blob).Value = data;
            command.ExecuteNonQuery();
        }
    }

    private static byte[] BuildPattern()
    {
        var pattern = new byte[256];
        for (var i = 0; i < pattern.Length; i++)
            pattern[i] = (byte)((i * 131 + 7) & 0xFF);
        return pattern;
    }

    private readonly record struct ProbeResult(
        string StreamType, int FieldCount, long Bytes, long Allocated, long PeakDelta, bool Correct, string? Error);
}
