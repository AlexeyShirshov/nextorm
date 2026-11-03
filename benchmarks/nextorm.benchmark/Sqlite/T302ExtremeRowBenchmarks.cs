using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// #302 S8 supporting report: the cost of the extreme-row request on SQLite over a purpose-built
/// table with heavy ties (<see cref="RowCount"/> rows, <see cref="DistinctValues"/> distinct scores,
/// so the global <c>All</c> arm returns <c>RowCount / DistinctValues</c> rows).
/// <para>
/// The same class runs against the pre-#302 engine and the shipped one: the <c>All</c> request is a
/// ranking window there and an aggregate join-back here, while <c>One</c> is the window in both. The
/// matrix is the three shapes the plan asks for — no index, an index on the extreme key, and a
/// value-only predicate — plus the <c>One</c> contrast arm.
/// </para>
/// </summary>
[Config(typeof(NextormConfig))]
[MemoryDiagnoser]
[HideColumns("Job", "Runtime", "Error", "StdDev", "RatioSD")]
public abstract class T302ExtremeRowBenchmarkBase
{
    /// <summary>Rows in the benchmark table.</summary>
    protected const int RowCount = 20_000;

    /// <summary>Distinct extreme-key values, which fixes the tie fan-out at <c>RowCount / DistinctValues</c>.</summary>
    protected const int DistinctValues = 100;

    /// <summary>Expected size of a global <c>All</c> result.</summary>
    protected const int AllRows = RowCount / DistinctValues;

    /// <summary>Predicate threshold for the value-only arm (the top tenth of the key range).</summary>
    protected const int ValueOnlyThreshold = 90;

    private string _file = null!;
    private IDataContext _db = null!;
    private IPreparedQueryCommand<ExtremeRowEntity> _all = null!;
    private IPreparedQueryCommand<ExtremeRowEntity> _one = null!;
    private IPreparedQueryCommand<ExtremeRowEntity> _allValueOnly = null!;
    private int _sink;

    /// <summary>Whether the extreme key carries an index for this arm.</summary>
    protected abstract bool Indexed { get; }

    [GlobalSetup]
    public void Setup()
    {
        // A unique file per setup call: BenchmarkDotNet re-runs GlobalSetup per benchmark method, and a
        // fixed path would collide with the still-open connection of the previous instance.
        _file = Path.Combine(Path.GetTempPath(), $"nextorm-t302-extreme-{(Indexed ? "indexed" : "plain")}-{Guid.NewGuid():N}.db");

        using (var connection = new SqliteConnection($"Data Source={_file}"))
        {
            connection.Open();
            using (var ddl = connection.CreateCommand())
            {
                ddl.CommandText = "create table if not exists extreme_rows (id integer primary key, score integer not null, category text not null, payload text not null)";
                ddl.ExecuteNonQuery();
            }

            using (var transaction = connection.BeginTransaction())
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = "insert into extreme_rows (id, score, category, payload) values ($id, $score, $category, $payload)";
                var id = insert.CreateParameter(); id.ParameterName = "$id"; insert.Parameters.Add(id);
                var score = insert.CreateParameter(); score.ParameterName = "$score"; insert.Parameters.Add(score);
                var category = insert.CreateParameter(); category.ParameterName = "$category"; insert.Parameters.Add(category);
                var payload = insert.CreateParameter(); payload.ParameterName = "$payload"; insert.Parameters.Add(payload);
                for (var i = 1; i <= RowCount; i++)
                {
                    id.Value = i;
                    score.Value = i % DistinctValues;
                    category.Value = $"cat{i % 50}";
                    payload.Value = $"payload-{i}";
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            if (Indexed)
            {
                using var index = connection.CreateCommand();
                index.CommandText = "create index ix_extreme_rows_score on extreme_rows (score)";
                index.ExecuteNonQuery();
            }
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_file);
        _db = builder.CreateDataContext();

        var source = _db.From<ExtremeRowEntity>();
        _all = source.SelectWhereMax(x => x.Score, ExtremeRowTies.All).Prepare();
        _one = source.SelectWhereMax(x => x.Score, ExtremeRowTies.One).Prepare();
        _allValueOnly = _db.From<ExtremeRowEntity>()
            .Where(x => x.Score > ValueOnlyThreshold)
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .Prepare();

        var all = _db.ToList(_all);
        var one = _db.ToList(_one);
        var valueOnly = _db.ToList(_allValueOnly);
        if (all.Count != AllRows)
            throw new InvalidOperationException($"All expected {AllRows} rows, got {all.Count}");
        if (one.Count != 1)
            throw new InvalidOperationException($"One expected 1 row, got {one.Count}");
        if (valueOnly.Count != AllRows)
            throw new InvalidOperationException($"All(value-only) expected {AllRows} rows, got {valueOnly.Count}");
        if (_db.ToList(_all).Select(x => x.Score).Distinct().Single() != DistinctValues - 1)
            throw new InvalidOperationException("All did not keep the extreme key");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        if (File.Exists(_file))
            File.Delete(_file);
    }

    [Benchmark]
    [BenchmarkCategory("A_ExtremeRow")]
    public int All_Global() => _sink += _db.ToList(_all).Count;

    [Benchmark]
    [BenchmarkCategory("A_ExtremeRow")]
    public int All_ValueOnly() => _sink += _db.ToList(_allValueOnly).Count;

    [Benchmark]
    [BenchmarkCategory("A_ExtremeRow")]
    public int One_Global() => _sink += _db.ToList(_one).Count;
}

/// <summary>Extreme-row matrix without an index on the extreme key.</summary>
public class T302ExtremeRowPlainBenchmark : T302ExtremeRowBenchmarkBase
{
    /// <inheritdoc />
    protected override bool Indexed => false;
}

/// <summary>Extreme-row matrix with an index on the extreme key.</summary>
public class T302ExtremeRowIndexedBenchmark : T302ExtremeRowBenchmarkBase
{
    /// <inheritdoc />
    protected override bool Indexed => true;
}

/// <summary>The benchmark table row.</summary>
[Table("extreme_rows")]
public sealed class ExtremeRowEntity
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Extreme key; carries <see cref="T302ExtremeRowBenchmarkBase.DistinctValues"/> distinct values.</summary>
    [Column("score")]
    public int Score { get; set; }

    /// <summary>Grouping column (unused by the global arms, kept for shape parity).</summary>
    [Column("category")]
    public string Category { get; set; } = "";

    /// <summary>Payload column, materialised with the surviving row.</summary>
    [Column("payload")]
    public string Payload { get; set; } = "";
}
