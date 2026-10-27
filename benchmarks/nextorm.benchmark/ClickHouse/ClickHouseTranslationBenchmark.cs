using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Benchmark;

// ---- Entities used by the ClickHouse translation/preparation baseline (T286b, issue #286) ----

[SqlTable("t286b_scalar")]
public interface IT286bScalarEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }

    [Column("value")]
    double Value { get; set; }
}

[SqlTable("t286b_array")]
public interface IT286bArrayEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("nums")]
    long[] Nums { get; set; }
}

/// <summary>
/// T286b (issue #286) D0 performance baseline: ClickHouse <b>translation and preparation</b> only.
/// <para>
/// The task extends the shared dispatch (<c>NormSqlTranslator</c>), the native function name table
/// (<c>ClickHouseNativeFunctionTranslator</c>) and the dialect renderer
/// (<c>ClickHouseScalarFunctions.Render</c>). Every scalar call in the query tree is probed by the
/// ClickHouse translator first, so the candidate must be compared against this post-T276 baseline on
/// the same scenarios, inputs, host conditions and warm-up/iteration protocol.
/// </para>
/// <para>
/// No SQL is executed: the context carries a placeholder connection string, every arm builds a
/// <see cref="QueryCommand{T}"/> and calls <c>GetPreparedQueryCommand</c>, and the rendered
/// <c>CommandText</c> length is returned as a sink. Client overhead and database time are therefore
/// separated by construction — database time is zero.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("clickhouse-translation", "t286b")]
[Config(typeof(NextormConfig))]
public class ClickHouseTranslationBenchmark
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";

    private readonly IDataContext _ctx;
    private readonly Func<string> _cachedArm;

    private int _sink;

    public ClickHouseTranslationBenchmark()
    {
        _ctx = new ClickHouseDataContext(PlaceholderConnectionString, new DataContextBuilder());

        // The same query shape is prepared twice: the first preparation stores the plan, so the arm
        // measures the cache-hit path (parameter-only pass) rather than a fresh translation.
        _cachedArm = () => Prepare(
            _ctx.From<IT286bScalarEntity>().Select(x => new { x.Id, U = SqlFunctions.ClickHouse.lower_utf8(x.Name) }),
            storeInCache: true);

        var controlSql = Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new { x.Id }));
        var cachedSql = _cachedArm();
        Console.WriteLine($"t286b-control-sql-len={controlSql.Length} cached-sql-len={cachedSql.Length}");
    }

    private string Prepare<T>(QueryCommand<T> command, bool storeInCache = false)
        => ((DbPreparedQueryCommand<T>)_ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None))
            .DbCommand.CommandText;

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    /// <summary>Baseline: query construction and preparation of an unaffected projection.</summary>
    [Benchmark(Baseline = true)]
    public int Control_PlainProjection_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new { x.Id }))).Length;

    /// <summary>Control: the plan-cache hit path for a query that already carries a native function.</summary>
    [Benchmark]
    public int Control_CachedPlan_Prepare()
        => _sink = Normalize(_cachedArm()).Length;

    /// <summary>Representative scalar families: string (UTF-8/regexp) plus numeric (log2).</summary>
    [Benchmark]
    public int ScalarFamily_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new
        {
            L = SqlFunctions.ClickHouse.lower_utf8(x.Name),
            R = SqlFunctions.ClickHouse.replace_regexp_all(x.Name, "a", "b"),
            M = SqlFunctions.ClickHouse.match(x.Name, "a"),
            G = SqlFunctions.ClickHouse.log2(x.Value)
        }))).Length;

    /// <summary>Dispatch-free control: the dialect renderer on its own (no query tree, no translator).</summary>
    [Benchmark]
    public int ScalarRender_DirectArms()
    {
        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;
        var len = 0;
        for (var i = 0; i < 100; i++)
        {
            len = scalars.Render("lower_utf8", ["name"]).Length
                + scalars.Render("replace_regexp_all", ["name", "'a'", "'b'"]).Length
                + scalars.Render("match", ["name", "'a'"]).Length
                + scalars.Render("log2", ["value"]).Length;
        }

        return _sink = len;
    }

    /// <summary>Array and higher-order lambda translation of a column argument.</summary>
    [Benchmark]
    public int ArrayHigherOrder_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bArrayEntity>().Select(x => new
        {
            Map = SqlFunctions.ClickHouse.array_map(v => v * 2, x.Nums),
            Filter = SqlFunctions.ClickHouse.array_filter(v => v > 2, x.Nums)
        }))).Length;

    /// <summary>Map family translation, including the map-lambda argument order.</summary>
    [Benchmark]
    public int Map_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new
        {
            F = SqlFunctions.ClickHouse.length(
                SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map(Tuple.Create("a", x.Id)))),
            S = SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(Tuple.Create("a", x.Id)))
        }))).Length;

    /// <summary>Aggregate and window translation: the shared aggregate/window dispatch paths.</summary>
    [Benchmark]
    public int AggregateWindow_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new
        {
            M = SqlFunctions.ClickHouse.arg_min(x.Id, x.Value),
            Q = SqlFunctions.ClickHouse.quantile(0.5, x.Value),
            R = SqlFunctions.Sql.row_number().Over(SqlFunctions.Sql.asc(() => x.Id))
        }))).Length;

    /// <summary>Dictionary translation through <c>DictionarySqlTranslator</c>.</summary>
    [Benchmark]
    public int Dictionary_Prepare()
        => _sink = Normalize(Prepare(_ctx.From<IT286bScalarEntity>().Select(x => new
        {
            V = SqlFunctions.ClickHouse.dict_get<string, long>("dict", "attr", x.Id),
            D = SqlFunctions.ClickHouse.dict_get_or_default<string, long>("dict", "attr", x.Id, "n/a"),
            H = SqlFunctions.ClickHouse.dict_has<long>("dict", x.Id)
        }))).Length;
}
