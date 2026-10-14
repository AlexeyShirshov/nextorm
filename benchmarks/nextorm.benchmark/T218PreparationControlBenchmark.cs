using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Benchmark;

/// <summary>
/// T218 (#218) preparation-path benchmark (category <c>t218-preparation</c>, deliberately NOT
/// <c>acceptance</c>). It measures the SQL Server-only scalar-function preparation path through the
/// <c>SqlServerScalarFunctionTranslator</c> argument loop and the <c>SqlServerSpecificFunctions.Render</c>
/// dispatch by preparing the query UNCACHED (<c>storeInCache: false</c>) so every iteration re-runs the
/// visitor and the renderer. The benchmark never opens a connection: only SQL text is produced.
/// </summary>
/// <remarks>
/// <c>patindex</c> is the unchanged control for the T218 sequence work: a post-change run must stay
/// within noise of the pre-change baseline (plan: mean ≤105% and allocations not above baseline). The
/// four new sequence cases (both aliases × one/two name components) are reported as an absolute
/// time/allocation report with no invented pre-feature baseline.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T218PreparationControlBenchmark
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;

    public T218PreparationControlBenchmark()
    {
        var builder = new DataContextBuilder();
        _db = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            builder);
        _repo = new TestDataRepository(_db);

        // Fail loudly on a broken harness (not on the measured path): prepare each query once.
        _ = ((DbPreparedQueryCommand<T218PreparationRow>)_db.GetPreparedQueryCommand(
            NewControlQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
        _ = ((DbPreparedQueryCommand<T218PreparationRow>)_db.GetPreparedQueryCommand(
            NewNextValueForBareQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
        _ = ((DbPreparedQueryCommand<T218PreparationRow>)_db.GetPreparedQueryCommand(
            NewNextValueForSchemaQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
        _ = ((DbPreparedQueryCommand<T218PreparationRow>)_db.GetPreparedQueryCommand(
            NewNextvalBareQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
        _ = ((DbPreparedQueryCommand<T218PreparationRow>)_db.GetPreparedQueryCommand(
            NewNextvalSchemaQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
    }

    /// <summary>Uncached preparation of a projection over the unchanged <c>patindex</c> scalar (control).</summary>
    /// <returns>The last prepared command; assigned to defeat dead-code elimination.</returns>
    [Benchmark]
    [BenchmarkCategory("t218-preparation")]
    public IPreparedQueryCommand<T218PreparationRow> Prepare_Control_Patindex_Uncached()
        => Prepare(NewControlQuery);

    /// <summary>Uncached preparation of the new <c>next_value_for</c> bare (one-component) sequence name.</summary>
    /// <returns>The last prepared command; assigned to defeat dead-code elimination.</returns>
    [Benchmark]
    [BenchmarkCategory("t218-preparation")]
    public IPreparedQueryCommand<T218PreparationRow> Prepare_NextValueFor_Bare_Uncached()
        => Prepare(NewNextValueForBareQuery);

    /// <summary>Uncached preparation of the new <c>next_value_for</c> schema-qualified (two-component) name.</summary>
    /// <returns>The last prepared command; assigned to defeat dead-code elimination.</returns>
    [Benchmark]
    [BenchmarkCategory("t218-preparation")]
    public IPreparedQueryCommand<T218PreparationRow> Prepare_NextValueFor_Schema_Uncached()
        => Prepare(NewNextValueForSchemaQuery);

    /// <summary>Uncached preparation of the new <c>nextval</c> alias bare (one-component) sequence name.</summary>
    /// <returns>The last prepared command; assigned to defeat dead-code elimination.</returns>
    [Benchmark]
    [BenchmarkCategory("t218-preparation")]
    public IPreparedQueryCommand<T218PreparationRow> Prepare_Nextval_Bare_Uncached()
        => Prepare(NewNextvalBareQuery);

    /// <summary>Uncached preparation of the new <c>nextval</c> alias schema-qualified (two-component) name.</summary>
    /// <returns>The last prepared command; assigned to defeat dead-code elimination.</returns>
    [Benchmark]
    [BenchmarkCategory("t218-preparation")]
    public IPreparedQueryCommand<T218PreparationRow> Prepare_Nextval_Schema_Uncached()
        => Prepare(NewNextvalSchemaQuery);

    // The query is rebuilt inside the loop (the same shape as the pre-change control), so the measured
    // path is identical before and after the T218 change.
    private IPreparedQueryCommand<T218PreparationRow> Prepare(Func<QueryCommand<T218PreparationRow>> factory)
    {
        IPreparedQueryCommand<T218PreparationRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(factory(), false, false, CancellationToken.None);
        return r!;
    }

    private QueryCommand<T218PreparationRow> NewControlQuery()
        => _repo.SimpleEntity
            .Where(x => x.Id == x.Id)
            .Select(x => new T218PreparationRow
            {
                Control = SqlFunctions.SqlServer.patindex("%a%", "abc"),
            });

    // The four new sequence cases: both aliases × one/two name components. The names are compile-time
    // literals, as the sequence marker requires.
    private QueryCommand<T218PreparationRow> NewNextValueForBareQuery()
        => _repo.SimpleEntity
            .Where(x => x.Id == x.Id)
            .Select(x => new T218PreparationRow
            {
                NextValueForBare = SqlFunctions.SqlServer.next_value_for("prep_seq"),
            });

    private QueryCommand<T218PreparationRow> NewNextValueForSchemaQuery()
        => _repo.SimpleEntity
            .Where(x => x.Id == x.Id)
            .Select(x => new T218PreparationRow
            {
                NextValueForSchema = SqlFunctions.SqlServer.next_value_for("dbo.prep_seq"),
            });

    private QueryCommand<T218PreparationRow> NewNextvalBareQuery()
        => _repo.SimpleEntity
            .Where(x => x.Id == x.Id)
            .Select(x => new T218PreparationRow
            {
                NextvalBare = SqlFunctions.SqlServer.nextval("prep_seq"),
            });

    private QueryCommand<T218PreparationRow> NewNextvalSchemaQuery()
        => _repo.SimpleEntity
            .Where(x => x.Id == x.Id)
            .Select(x => new T218PreparationRow
            {
                NextvalSchema = SqlFunctions.SqlServer.nextval("dbo.prep_seq"),
            });
}

/// <summary>Projection row for the T218 control/sequence preparation benchmarks.</summary>
public sealed class T218PreparationRow
{
    /// <summary>The unchanged <c>patindex</c> control projection.</summary>
    public int? Control { get; set; }

    /// <summary>The new <c>next_value_for</c> bare sequence projection.</summary>
    public long NextValueForBare { get; set; }

    /// <summary>The new <c>next_value_for</c> schema-qualified sequence projection.</summary>
    public long NextValueForSchema { get; set; }

    /// <summary>The new <c>nextval</c> bare sequence projection.</summary>
    public long NextvalBare { get; set; }

    /// <summary>The new <c>nextval</c> schema-qualified sequence projection.</summary>
    public long NextvalSchema { get; set; }
}
