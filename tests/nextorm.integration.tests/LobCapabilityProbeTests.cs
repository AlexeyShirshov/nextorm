using System.Data;
using System.Text;
using MySqlConnector;

namespace NextORM.Integration.Tests;

/// <summary>
/// Capability probe (PDCA cycle 2, issue #27): does the MySqlConnector driver — shared by the
/// nextorm MySQL and MariaDB providers — read a large BLOB/CLOB <b>memory-bounded</b> through
/// <see cref="System.Data.Common.DbDataReader.GetStream(int)"/> /
/// <see cref="System.Data.Common.DbDataReader.GetTextReader(int)"/> under
/// <see cref="System.Data.CommandBehavior.SequentialAccess"/>.
/// <para>
/// The driver is used directly (<see cref="MySqlConnection"/>/<see cref="MySqlCommand"/>), so the
/// nextorm LOB terminals and their guard are bypassed. For each provider it seeds a 1 MiB and an
/// 8 MiB row, reads both through a fixed 64 KiB buffer, verifies the content and reports the
/// best-of-3 <see cref="GC.GetAllocatedBytesForCurrentThread"/> delta. A memory-bounded read has
/// <c>alloc(8 MiB) / alloc(1 MiB) ≈ 1</c>; a buffered read shows <c>≈ 8</c>. The same read is
/// repeated without <see cref="System.Data.CommandBehavior.SequentialAccess"/> to document whether
/// the mode matters.
/// </para>
/// <para>
/// Out-of-band probe: it only runs when <c>NEXTORM_LOB_PROBE=1</c> is set, so the normal integration
/// suite does not pay for the extra seeds and the repeated multi-megabyte reads.
/// </para>
/// </summary>
public sealed class LobCapabilityProbeTests
{
    private const int MiB = 1024 * 1024;
    private const int BufferChars = 64 * 1024;
    private const int Samples = 3;

    private static string OutputPath =>
        Environment.GetEnvironmentVariable("NEXTORM_LOB_PROBE_LOG") ?? "/tmp/lob-capability-probe.log";

    private static readonly object LogGate = new();

    [Fact]
    public void MySql_Driver_Reads_Lobs_MemoryBounded()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("NEXTORM_LOB_PROBE") == "1",
            "Set NEXTORM_LOB_PROBE=1 to run the LOB capability probe.");
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        RunProbe("mysql", MySqlContainer.ConnectionString);
    }

    [Fact]
    public void MariaDb_Driver_Reads_Lobs_MemoryBounded()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("NEXTORM_LOB_PROBE") == "1",
            "Set NEXTORM_LOB_PROBE=1 to run the LOB capability probe.");
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        RunProbe("mariadb", MariaDbContainer.ConnectionString);
    }

    private static void RunProbe(string provider, string connectionString)
    {
        const string table = "lob_capability_probe";

        var small = MakeBytes(MiB);
        var large = MakeBytes(8 * MiB);
        var smallText = MakeText(MiB);
        var largeText = MakeText(8 * MiB);

        using (var connection = new MySqlConnection(connectionString))
        {
            connection.Open();
            Seed(connection, table, small, large, smallText, largeText);
        }

        var binaryOn = MeasureBinary(connectionString, table, id: 2, sequential: true, large);
        var binaryOff = MeasureBinary(connectionString, table, id: 2, sequential: false, large);
        var binaryOnSmall = MeasureBinary(connectionString, table, id: 1, sequential: true, small);
        var binaryOffSmall = MeasureBinary(connectionString, table, id: 1, sequential: false, small);

        var textOn = MeasureText(connectionString, table, id: 2, sequential: true, largeText);
        var textOff = MeasureText(connectionString, table, id: 2, sequential: false, largeText);
        var textOnSmall = MeasureText(connectionString, table, id: 1, sequential: true, smallText);
        var textOffSmall = MeasureText(connectionString, table, id: 1, sequential: false, smallText);

        var lines = new List<string>
        {
            $"# LOB capability probe [{provider}]; driver MySqlConnector 2.6.2; runtime {Environment.Version}",
            $"# provider | primitive | sequential | alloc(1 MiB) | alloc(8 MiB) | ratio | correct",
        };

        AppendLine(lines, provider, "BLOB GetStream", "on", binaryOnSmall, binaryOn);
        AppendLine(lines, provider, "BLOB GetStream", "off", binaryOffSmall, binaryOff);
        AppendLine(lines, provider, "CLOB GetTextReader", "on", textOnSmall, textOn);
        AppendLine(lines, provider, "CLOB GetTextReader", "off", textOffSmall, textOff);

        lines.Add($"[{provider}] VERDICT BLOB GetStream     seq=on : {Verdict(binaryOnSmall, binaryOn)}");
        lines.Add($"[{provider}] VERDICT BLOB GetStream     seq=off: {Verdict(binaryOffSmall, binaryOff)}");
        lines.Add($"[{provider}] VERDICT CLOB GetTextReader seq=on : {Verdict(textOnSmall, textOn)}");
        lines.Add($"[{provider}] VERDICT CLOB GetTextReader seq=off: {Verdict(textOffSmall, textOff)}");

        lock (LogGate)
        {
            File.AppendAllLines(OutputPath, lines);
        }
    }

    private static void AppendLine(
        List<string> lines, string provider, string primitive, string sequential,
        ProbeResult oneMiB, ProbeResult eightMiB)
    {
        var ratio = Ratio(oneMiB, eightMiB);
        var correct = oneMiB.Correct && eightMiB.Correct ? "ok" : "FAIL";
        var error = oneMiB.Error ?? eightMiB.Error;
        lines.Add(
            $"[{provider}] {primitive,-20} seq={sequential,-3} " +
            $"1MiB={oneMiB.Allocated,10} B 8MiB={eightMiB.Allocated,10} B ratio={ratio,-6} correct={correct}" +
            (error is null ? string.Empty : $" error={error}"));
    }

    private static string Verdict(ProbeResult oneMiB, ProbeResult eightMiB)
    {
        if (oneMiB.Error is not null || eightMiB.Error is not null)
            return $"unsupported ({oneMiB.Error ?? eightMiB.Error})";
        if (!oneMiB.Correct || !eightMiB.Correct)
            return "content mismatch";
        var ratio = Ratio(oneMiB, eightMiB);
        if (double.IsNaN(ratio))
            return "inconclusive";
        if (ratio <= 1.5)
            return $"memory-bounded (ratio {ratio:F2})";
        if (ratio >= 4.0)
            return $"buffered (ratio {ratio:F2})";
        return $"inconclusive (ratio {ratio:F2})";
    }

    private static double Ratio(ProbeResult oneMiB, ProbeResult eightMiB)
    {
        if (oneMiB.Allocated <= 0 || eightMiB.Allocated <= 0)
            return double.NaN;
        return (double)eightMiB.Allocated / oneMiB.Allocated;
    }

    private static ProbeResult MeasureBinary(
        string connectionString, string table, int id, bool sequential, byte[] expected)
    {
        var expectedSum = Sum(expected);
        var behavior = sequential ? CommandBehavior.SequentialAccess : CommandBehavior.Default;
        var buffer = new byte[BufferChars];

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            (long Length, long Sum) ReadOnce()
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"select data from {table} where id = @id";
                command.Parameters.AddWithValue("@id", id);
                using var reader = command.ExecuteReader(behavior);
                if (!reader.Read())
                    throw new InvalidOperationException("the seeded row was not returned");

                using var stream = reader.GetStream(0);
                long length = 0;
                long sum = 0;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < read; i++)
                        sum += buffer[i];
                    length += read;
                }

                return (length, sum);
            }

            // Warm up JIT and any per-connection state before measuring.
            ReadOnce();
            ReadOnce();

            var best = long.MaxValue;
            (long Length, long Sum) last = default;
            for (var i = 0; i < Samples; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                last = ReadOnce();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated < best)
                    best = allocated;
            }

            var correct = last.Length == expected.Length && last.Sum == expectedSum;
            return new ProbeResult(best, correct, correct ? null : $"length={last.Length}/{expected.Length} sum={last.Sum}/{expectedSum}");
        }
        catch (Exception exception)
        {
            return new ProbeResult(-1, false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static ProbeResult MeasureText(
        string connectionString, string table, int id, bool sequential, string expected)
    {
        var expectedSum = Sum(expected);
        var behavior = sequential ? CommandBehavior.SequentialAccess : CommandBehavior.Default;
        var buffer = new char[BufferChars];

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            (long Length, long Sum) ReadOnce()
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"select body from {table} where id = @id";
                command.Parameters.AddWithValue("@id", id);
                using var reader = command.ExecuteReader(behavior);
                if (!reader.Read())
                    throw new InvalidOperationException("the seeded row was not returned");

                using var textReader = reader.GetTextReader(0);
                long length = 0;
                long sum = 0;
                int read;
                while ((read = textReader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < read; i++)
                        sum += buffer[i];
                    length += read;
                }

                return (length, sum);
            }

            ReadOnce();
            ReadOnce();

            var best = long.MaxValue;
            (long Length, long Sum) last = default;
            for (var i = 0; i < Samples; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                last = ReadOnce();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated < best)
                    best = allocated;
            }

            var correct = last.Length == expected.Length && last.Sum == expectedSum;
            return new ProbeResult(best, correct, correct ? null : $"length={last.Length}/{expected.Length} sum={last.Sum}/{expectedSum}");
        }
        catch (Exception exception)
        {
            return new ProbeResult(-1, false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void Seed(
        MySqlConnection connection, string table,
        byte[] small, byte[] large, string smallText, string largeText)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"drop table if exists {table}";
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"create table {table} (id int primary key, data longblob not null, body longtext not null)";
            command.ExecuteNonQuery();
        }

        Insert(connection, table, 1, small, smallText);
        InsertChunked(connection, table, 2, large, largeText, MiB);
    }

    private static void Insert(MySqlConnection connection, string table, int id, byte[] data, string body)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"insert into {table} (id, data, body) values (@id, @data, @body)";
        command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32) { Value = id });
        command.Parameters.Add(new MySqlParameter("@data", MySqlDbType.LongBlob) { Value = data });
        command.Parameters.Add(new MySqlParameter("@body", MySqlDbType.LongText) { Value = body });
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Seeds a row without ever sending a client packet larger than <paramref name="chunkSize"/>:
    /// MariaDB's default <c>max_allowed_packet</c> (16 MiB) rejects an 8 MiB blob + 8 MiB text row
    /// in one statement. The value is grown server-side with <c>CONCAT</c> instead, which keeps the
    /// stored column byte-identical while each parameter stays small.
    /// </summary>
    private static void InsertChunked(
        MySqlConnection connection, string table, int id, byte[] data, string body, int chunkSize)
    {
        var total = Math.Max(data.Length, body.Length);
        Insert(connection, table, id,
            data.Length > chunkSize ? data[..chunkSize] : data,
            body.Length > chunkSize ? body[..chunkSize] : body);

        for (var offset = chunkSize; offset < total; offset += chunkSize)
        {
            var dataLength = Math.Min(chunkSize, data.Length - offset);
            var bodyLength = Math.Min(chunkSize, body.Length - offset);

            using var command = connection.CreateCommand();
            command.CommandText =
                $"update {table} set data = concat(data, @data), body = concat(body, @body) where id = @id";
            command.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32) { Value = id });
            command.Parameters.Add(new MySqlParameter("@data", MySqlDbType.LongBlob)
            {
                Value = dataLength > 0 ? data.AsSpan(offset, dataLength).ToArray() : Array.Empty<byte>(),
            });
            command.Parameters.Add(new MySqlParameter("@body", MySqlDbType.LongText)
            {
                Value = bodyLength > 0 ? body.Substring(offset, bodyLength) : string.Empty,
            });
            command.ExecuteNonQuery();
        }
    }

    private static byte[] MakeBytes(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
            data[i] = (byte)((i * 131 + 7) & 0xFF);
        return data;
    }

    private static string MakeText(int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            builder.Append((char)('a' + (i % 26)));
        return builder.ToString();
    }

    private static long Sum(byte[] data)
    {
        long sum = 0;
        foreach (var value in data)
            sum += value;
        return sum;
    }

    private static long Sum(string text)
    {
        long sum = 0;
        foreach (var value in text)
            sum += value;
        return sum;
    }

    private readonly record struct ProbeResult(long Allocated, bool Correct, string? Error);
}
