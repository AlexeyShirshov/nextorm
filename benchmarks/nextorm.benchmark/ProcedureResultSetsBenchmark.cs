using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Compares the legacy sequential raw reader (<c>Read&lt;T&gt;()</c> twice /
/// <c>ReadAsync&lt;T&gt;</c> twice) with the newer one-shot result-set traversal
/// (<c>ReadSets()</c> / <c>ReadSetsAsync(ct)</c> plus each cursor's <c>Read&lt;T&gt;</c> /
/// <c>ReadAsync&lt;T&gt;</c>) over two column-bearing sets of the same statement on an in-memory
/// SQLite connection.
/// </summary>
/// <remarks>
/// Both arms execute the identical multi-statement SQL (<c>select …; select …</c>) and consume the same
/// rows in the same order, so the DB cost cancels out and the delta is the traversal overhead. The new
/// sync arm is eager like the legacy one; the new async arm streams per row through
/// <see cref="ResultSet.ReadAsync{T}"/> instead of buffering a whole set. <see cref="Setup"/> guards the
/// benchmark by asserting that all four arms fold to the same row checksum.
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("procedure-sets")]
public class ProcedureResultSetsBenchmark
{
    private const string SelectSetsSql =
        "select id from procedure_sets_bench; select value from procedure_sets_bench";

    [Params(64, 8192)]
    public int Rows { get; set; }

    private SqliteConnection _connection = null!;
    private IDataContext _db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using (var seed = _connection.CreateCommand())
        {
            seed.CommandText = "create table procedure_sets_bench (id integer not null, value integer not null)";
            seed.ExecuteNonQuery();

            seed.CommandText = "insert into procedure_sets_bench (id, value) values (@id, @value)";
            var id = seed.CreateParameter();
            id.ParameterName = "@id";
            seed.Parameters.Add(id);
            var value = seed.CreateParameter();
            value.ParameterName = "@value";
            seed.Parameters.Add(value);

            using var tx = _connection.BeginTransaction();
            seed.Transaction = tx;
            for (var i = 0; i < Rows; i++)
            {
                id.Value = i;
                value.Value = i * 3 + 1;
                seed.ExecuteNonQuery();
            }

            tx.Commit();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_connection);
        _db = builder.CreateDataContext();

        VerifyChecksumsAgree();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db?.Dispose();
        _connection?.Dispose();
    }

    // Legacy sync: two eager Read<T> calls, each advancing to the next set.
    [Benchmark(Baseline = true)]
    public long Legacy_Sync() => RunLegacySync();

    // New sync traversal: eager per-set Read<T>, outer ReadSets().
    [Benchmark]
    public long Traversal_Sync() => RunTraversalSync();

    // Legacy async: two streaming ReadAsync<T> enumerations.
    [Benchmark]
    public Task<long> Legacy_Async() => RunLegacyAsync();

    // New async traversal: outer ReadSetsAsync(ct), streaming per-set ReadAsync<T>.
    [Benchmark]
    public Task<long> Traversal_Async() => RunTraversalAsync();

    private long RunLegacySync()
    {
        using var result = _db.ExecuteRaw(SelectSetsSql);
        var checksum = FoldRows(result.Read<int>(), 0);
        return FoldRows(result.Read<int>(), checksum);
    }

    private long RunTraversalSync()
    {
        using var result = _db.ExecuteRaw(SelectSetsSql);
        var checksum = 0L;
        foreach (var set in result.ReadSets())
            checksum = FoldRows(set.Read<int>(), checksum);

        return checksum;
    }

    private async Task<long> RunLegacyAsync()
    {
        await using var result = await _db.ExecuteRawAsync(SelectSetsSql);
        var checksum = 0L;
        await foreach (var row in result.ReadAsync<int>())
            checksum = Mix(checksum, row);
        await foreach (var row in result.ReadAsync<int>())
            checksum = Mix(checksum, row);

        return checksum;
    }

    private async Task<long> RunTraversalAsync()
    {
        await using var result = await _db.ExecuteRawAsync(SelectSetsSql);
        var checksum = 0L;
        await foreach (var set in result.ReadSetsAsync())
        {
            await foreach (var row in set.ReadAsync<int>())
                checksum = Mix(checksum, row);
        }

        return checksum;
    }

    private void VerifyChecksumsAgree()
    {
        var legacySync = RunLegacySync();
        var traversalSync = RunTraversalSync();
        var legacyAsync = RunLegacyAsync().GetAwaiter().GetResult();
        var traversalAsync = RunTraversalAsync().GetAwaiter().GetResult();

        if (legacySync != traversalSync || legacySync != legacyAsync || legacySync != traversalAsync)
        {
            throw new InvalidOperationException(
                $"[procedure-sets] checksum mismatch for Rows={Rows}: legacy-sync={legacySync}, "
                + $"traversal-sync={traversalSync}, legacy-async={legacyAsync}, traversal-async={traversalAsync}");
        }

        Console.WriteLine($"[procedure-sets] rows={Rows} rows-per-set={Rows} checksum={legacySync} (legacy sync/async == traversal sync/async)");
    }

    private static long FoldRows(IReadOnlyList<int> rows, long checksum)
    {
        for (var i = 0; i < rows.Count; i++)
            checksum = Mix(checksum, rows[i]);

        return checksum;
    }

    private static long Mix(long checksum, long value) => unchecked(checksum * 31 + value);
}
