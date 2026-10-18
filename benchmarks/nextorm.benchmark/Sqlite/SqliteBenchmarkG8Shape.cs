using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T297-G8 exact-shape construction probe. It measures the allocations of the REAL CTE chains that
/// the acceptance oracle executes (construct only; nothing is prepared, executed or sent to SQLite):
/// <list type="bullet">
/// <item><b>RecursiveCte</b> - <c>anchor.Select</c>, <c>step.Select</c>,
///   <c>anchor.UnionAll(step)</c>, <c>WithRecursive</c>, <c>From</c>, final <c>Select</c>.</item>
/// <item><b>Cte</b> - body <c>Select</c>, <c>With</c>, <c>From</c>, <c>Join</c>, final <c>Select</c>.</item>
/// </list>
/// Each probe is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> deltas around the
/// construction only (inputs of the one-call isolations are prepared before the counter is read), with
/// 1,000 warmups and 1,001 samples per probe. The raw sample list is exported from
/// <see cref="WriteEvidence"/> to <c>.pdca/T297/rv1/shape/steps.json</c> and <c>samples.csv</c>; the
/// per-case BenchmarkDotNet allocation column is an independent cross-check, not the evidence.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkG8Shape
{
    private const int WarmupSamples = 1000;
    private const int SampleCount = 1001;
    private const int ExpectedProbes = 32;

    // A pre-built projection expression: used only by the node pin that separates a command from a
    // QueryDefinition. It never replaces the inline lambdas of the exact-shape probes.
    private static readonly Expression<Func<SimpleEntity, CteNumberRow>> CachedSelectExpr =
        s => new CteNumberRow { n = s.Id };

    private static readonly Dictionary<string, ProbeResult> Results = new(StringComparer.Ordinal);
    private static readonly object ResultsGate = new();
    private static string? _lastProbe;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private long _sink;
    private int _setupVerified;

    public SqliteBenchmarkG8Shape()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
    }

    /// <summary>Raw samples of the most recently collected probe (diagnostics only).</summary>
    public static IReadOnlyList<long> LastSamples
    {
        get
        {
            lock (ResultsGate)
            {
                return _lastProbe is not null && Results.TryGetValue(_lastProbe, out var probe)
                    ? probe.SampleList
                    : Array.Empty<long>();
            }
        }
    }

    [GlobalSetup]
    public void Verify()
    {
        // Metadata warm-up only; no SQL is executed by this class.
        _ = _db.From<SimpleEntity>();
        _ = _db.From("nums");

        if (Interlocked.Exchange(ref _setupVerified, 1) == 0)
        {
            var recursive = BuildRecursiveWhole();
            var cte = BuildCteWhole();
            if (recursive.Ctes?.Count != 1)
                throw new InvalidOperationException($"RecursiveCte chain must declare 1 CTE, got {recursive.Ctes?.Count ?? -1}.");
            if (recursive.UnionQuery is not null)
                throw new InvalidOperationException("RecursiveCte consumer must not carry its own set operation.");
            if (cte.Ctes?.Count != 1)
                throw new InvalidOperationException($"Cte chain must declare 1 CTE, got {cte.Ctes?.Count ?? -1}.");

            var body = BuildRecursiveBody();
            if (body.UnionQuery is null || body.UnionType != UnionType.All)
                throw new InvalidOperationException("RecursiveCte body must be a UNION ALL command.");

            Console.WriteLine($"SqliteBenchmarkG8Shape verification passed: Cte.Ctes.Count={cte.Ctes!.Count}, "
                + $"RecursiveCte.Ctes.Count={recursive.Ctes!.Count}, UnionType={body.UnionType}");
        }
    }

    [GlobalCleanup]
    public void WriteEvidence()
    {
        Dictionary<string, ProbeResult> snapshot;
        lock (ResultsGate)
        {
            snapshot = new Dictionary<string, ProbeResult>(Results, StringComparer.Ordinal);
            _lastProbe = null;
        }

        var evidenceDir = ResolveEvidenceDir();
        Directory.CreateDirectory(evidenceDir);

        var payload = new EvidencePayload
        {
            Task = "T297-g8-shape",
            GeneratedUtc = DateTime.UtcNow.ToString("O"),
            Method = "GC.GetAllocatedBytesForCurrentThread delta around construction only; inputs of isolations prepared before the counter is read",
            Warmup = WarmupSamples,
            Samples = SampleCount,
            ProbesCollected = snapshot.Count,
            ProbesExpected = ExpectedProbes,
            Complete = snapshot.Count >= ExpectedProbes,
            Probes = snapshot,
            Closure = new Dictionary<string, ClosureCheck>
            {
                ["recursive"] = BuildClosure(snapshot, "recursive", "rec.whole",
                    ("rec.anchor", "anchor"), ("rec.iso.step", "step"), ("rec.iso.union", "unionall"),
                    ("rec.iso.with_recursive", "with_recursive"), ("rec.iso.from", "from"),
                    ("rec.iso.final_select", "final_select")),
                ["cte"] = BuildClosure(snapshot, "cte", "cte.whole",
                    ("cte.body", "body"), ("cte.iso.with", "with"), ("cte.iso.from", "from"),
                    ("cte.iso.join", "join"), ("cte.iso.final_select", "final_select")),
            },
        };

        File.WriteAllText(Path.Combine(evidenceDir, "steps.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var csv = new StringBuilder("probe,sample,bytes\n");
        foreach (var probe in snapshot.Values.OrderBy(p => p.Name, StringComparer.Ordinal))
            for (var i = 0; i < probe.SampleList.Length; i++)
                csv.Append(probe.Name).Append(',').Append(i).Append(',').Append(probe.SampleList[i]).Append('\n');
        File.WriteAllText(Path.Combine(evidenceDir, "samples.csv"), csv.ToString());

        Console.WriteLine($"SqliteBenchmarkG8Shape evidence: {snapshot.Count}/{ExpectedProbes} probes -> {evidenceDir}");
    }

    // ---------------------------------------------------------------------------------------------
    // Probe-only single-process driver (Program.cs, NEXTORM_G8_PROBE_ONLY=1).
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs every probe body exactly once in this process and exports the evidence.
    /// BenchmarkDotNet launches one process per benchmark, so the static collector can never observe
    /// all 32 probes in a BDN run; this entry point is the probe-only path (no BDN job, no config,
    /// attribute or measured-body change). Returns 0 only when 32/32 probes with 1001 samples each
    /// and a 32033-line <c>samples.csv</c> were written; otherwise it prints one diagnostic per
    /// failure and returns 1.
    /// </summary>
    public static int RunAllProbes()
    {
        var instance = new SqliteBenchmarkG8Shape();
        instance.Verify();
        instance.InvokeProbeBodies();
        instance.WriteEvidence();
        return VerifyEvidence(ResolveEvidenceDir());
    }

    // One entry per [Benchmark] body, in declaration order. Each body calls Collect exactly once;
    // Collect itself is idempotent per key, so nothing here samples a probe twice.
    private static readonly Action<SqliteBenchmarkG8Shape>[] ProbeBodies =
    {
        b => b.G8_Cte_Whole(),
        b => b.G8_RecursiveCte_Whole(),
        b => b.G8_Recursive_Anchor(),
        b => b.G8_Recursive_Anchor_Step(),
        b => b.G8_Recursive_UnionBody(),
        b => b.G8_Cte_Body(),
        b => b.G8_Cte_Body_With(),
        b => b.G8_Cte_Body_With_From(),
        b => b.G8_Cte_Body_With_From_Join(),
        b => b.G8_Iso_UnionAll(),
        b => b.G8_Iso_FinalSelect(),
        b => b.G8_Iso_With(),
        b => b.G8_Iso_WithRecursive(),
        b => b.G8_Iso_From(),
        b => b.G8_Iso_Cte_From(),
        b => b.G8_Iso_Cte_Join(),
        b => b.G8_Iso_Cte_FinalSelect(),
        b => b.G8_Iso_Step(),
        b => b.G8_Iso_SelectCached(),
        b => b.G8_Node_SelectCachedFreshBuilder(),
        b => b.G8_Node_Definition(),
        b => b.G8_Node_FromEntity(),
        b => b.G8_Node_ToCommandFreshBuilder(),
        b => b.G8_Node_SelectInline(),
        b => b.G8_Node_AnchorToCommand(),
        b => b.G8_Node_StepToCommand(),
        b => b.G8_Node_BodyToCommand(),
        b => b.G8_Cte_Whole_ToCommand(),
        b => b.G8_RecursiveCte_Whole_ToCommand(),
        b => b.G8_Control_Noop(),
        b => b.G8_Control_Object(),
        b => b.G8_Control_Builder(),
    };

    private void InvokeProbeBodies()
    {
        var limit = ProbeLimit();
        for (var i = 0; i < ProbeBodies.Length && i < limit; i++) ProbeBodies[i](this);
    }

    // Diagnostic-only truncation for the completeness negative check: NEXTORM_G8_PROBE_LIMIT=<n>
    // runs only the first n probe bodies. It never changes a sampler, a measured body or a threshold.
    private static int ProbeLimit()
    {
        var raw = Environment.GetEnvironmentVariable("NEXTORM_G8_PROBE_LIMIT");
        return int.TryParse(raw, out var limit) && limit > 0 && limit < ProbeBodies.Length
            ? limit
            : ProbeBodies.Length;
    }

    private static int VerifyEvidence(string evidenceDir)
    {
        var failures = new List<string>();
        lock (ResultsGate)
        {
            if (Results.Count != ExpectedProbes)
                failures.Add($"probes collected: {Results.Count} (expected {ExpectedProbes})");
            foreach (var probe in Results.Values)
            {
                if (probe.SampleList.Length != SampleCount)
                    failures.Add($"{probe.Name}: {probe.SampleList.Length} samples (expected {SampleCount})");
            }
        }

        var csvPath = Path.Combine(evidenceDir, "samples.csv");
        var csvLines = File.Exists(csvPath) ? File.ReadAllText(csvPath).Count(ch => ch == '\n') : -1;
        var expectedLines = 1 + ExpectedProbes * SampleCount;
        if (csvLines != expectedLines)
            failures.Add($"samples.csv: {csvLines} lines (expected {expectedLines})");

        if (failures.Count == 0)
        {
            Console.WriteLine($"SqliteBenchmarkG8Shape probe-only run complete: {ExpectedProbes}/{ExpectedProbes} probes x {SampleCount} samples, samples.csv {csvLines} lines -> {evidenceDir}");
            return 0;
        }

        foreach (var failure in failures)
            Console.Error.WriteLine($"SqliteBenchmarkG8Shape probe-only FAIL: {failure}");
        return 1;
    }

    // ---------------------------------------------------------------------------------------------
    // Exact full-chain probes (construct only, no execution).
    // ---------------------------------------------------------------------------------------------

    [Benchmark]
    public QueryCommand<CteJoinRow> G8_Cte_Whole()
    {
        Collect("cte.whole", SampleCteWhole);
        return BuildCteWhole();
    }

    [Benchmark]
    public QueryCommand<int> G8_RecursiveCte_Whole()
    {
        Collect("rec.whole", SampleRecursiveWhole);
        return BuildRecursiveWhole();
    }

    // ---------------------------------------------------------------------------------------------
    // Prefix probes: RecursiveCte.
    // ---------------------------------------------------------------------------------------------

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Recursive_Anchor()
    {
        Collect("rec.anchor", SampleAnchor);
        return BuildAnchor();
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Recursive_Anchor_Step()
    {
        Collect("rec.anchor_step", SampleAnchorStep);
        return BuildStep();
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Recursive_UnionBody()
    {
        Collect("rec.union_body", SampleUnionBody);
        return BuildRecursiveBody();
    }

    // ---------------------------------------------------------------------------------------------
    // Prefix probes: Cte. The anonymous body projection cannot be named, so the prefix arms that stop
    // before the final Select return a structural witness (the returned object is never executed).
    // ---------------------------------------------------------------------------------------------

    [Benchmark]
    public QueryCommand G8_Cte_Body()
    {
        Collect("cte.body", SampleCteBody);
        return BuildCteBody();
    }

    [Benchmark]
    public CteQuery G8_Cte_Body_With()
    {
        Collect("cte.body_with", SampleCteBodyWith);
        return BuildCteScope();
    }

    [Benchmark]
    public EntityBuilder<TableAlias> G8_Cte_Body_With_From()
    {
        Collect("cte.body_with_from", SampleCteBodyWithFrom);
        return BuildCteFrom();
    }

    [Benchmark]
    public int G8_Cte_Body_With_From_Join()
    {
        Collect("cte.body_with_from_join", SampleCteBodyWithFromJoin);
        return BuildCteJoinCount();
    }

    // ---------------------------------------------------------------------------------------------
    // One-call isolations (inputs freshly prepared and untimed inside every sample).
    // ---------------------------------------------------------------------------------------------

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Iso_UnionAll()
    {
        Collect("rec.iso.union", SampleUnionIsolation);
        var anchor = BuildAnchor();
        var step = BuildStep();
        return anchor.UnionAll(step);
    }

    [Benchmark]
    public QueryCommand<int> G8_Iso_FinalSelect()
    {
        Collect("rec.iso.final_select", SampleRecursiveFinalSelectIsolation);
        var builder = _db.WithRecursive("nums", BuildRecursiveBody()).From("nums");
        return builder.Select(t => t["n"].AsInt);
    }

    [Benchmark]
    public CteQuery G8_Iso_With()
    {
        Collect("cte.iso.with", SampleCteWithIsolation);
        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return _db.With("recent", recent);
    }

    [Benchmark]
    public CteQuery G8_Iso_WithRecursive()
    {
        Collect("rec.iso.with_recursive", SampleWithRecursiveIsolation);
        return _db.WithRecursive("nums", BuildRecursiveBody());
    }

    [Benchmark]
    public EntityBuilder<TableAlias> G8_Iso_From()
    {
        Collect("rec.iso.from", SampleFromIsolation);
        return _db.WithRecursive("nums", BuildRecursiveBody()).From("nums");
    }

    [Benchmark]
    public EntityBuilder<TableAlias> G8_Iso_Cte_From()
    {
        Collect("cte.iso.from", SampleCteFromIsolation);
        return BuildCteFrom();
    }

    [Benchmark]
    public int G8_Iso_Cte_Join()
    {
        Collect("cte.iso.join", SampleCteJoinIsolation);
        return BuildCteJoinCount();
    }

    [Benchmark]
    public QueryCommand<CteJoinRow> G8_Iso_Cte_FinalSelect()
    {
        Collect("cte.iso.final_select", SampleCteFinalSelectIsolation);
        return BuildCteWhole();
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Iso_Step()
    {
        Collect("rec.iso.step", SampleStep);
        return BuildStep();
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Iso_SelectCached()
    {
        Collect("node.select_cached", SampleSelectCachedIsolation);
        var builder = _ctx.SimpleEntity.Where(s => s.Id == 1);
        return builder.Select(CachedSelectExpr);
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Node_SelectCachedFreshBuilder()
    {
        Collect("node.select_cached_fresh_builder", SampleSelectCachedFreshBuilder);
        return _db.From<SimpleEntity>().Select(CachedSelectExpr);
    }

    [Benchmark]
    public QueryDefinition G8_Node_Definition()
    {
        Collect("node.definition", SampleDefinition);
        return new QueryDefinition();
    }

    [Benchmark]
    public EntityBuilder<SimpleEntity> G8_Node_FromEntity()
    {
        Collect("node.from_entity", SampleFromEntity);
        return _db.From<SimpleEntity>();
    }

    [Benchmark]
    public QueryCommand<SimpleEntity> G8_Node_ToCommandFreshBuilder()
    {
        Collect("node.tocommand_fresh_builder", SampleToCommandFreshBuilder);
        return _db.From<SimpleEntity>().ToCommand();
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> G8_Node_SelectInline()
    {
        Collect("node.select_inline", SampleSelectInline);
        var builder = _ctx.SimpleEntity.Where(s => s.Id == 1);
        return builder.Select(s => new CteNumberRow { n = s.Id });
    }

    [Benchmark]
    public QueryCommand<SimpleEntity> G8_Node_AnchorToCommand()
    {
        Collect("node.anchor_tocommand", SampleAnchorToCommand);
        return _ctx.SimpleEntity.Where(s => s.Id == 1).ToCommand();
    }

    [Benchmark]
    public QueryCommand<TableAlias> G8_Node_StepToCommand()
    {
        Collect("node.step_tocommand", SampleStepToCommand);
        return _db.From("nums").Where(t => t["n"].AsInt < 5).ToCommand();
    }

    [Benchmark]
    public QueryCommand<ComplexEntity> G8_Node_BodyToCommand()
    {
        Collect("node.body_tocommand", SampleBodyToCommand);
        return _ctx.ComplexEntity.Where(c => c.Id > 1).ToCommand();
    }

    [Benchmark]
    public QueryCommand G8_Cte_Whole_ToCommand()
    {
        Collect("cte.whole_tocommand", SampleCteWholeToCommand);
        return BuildCteWholeToCommand();
    }

    [Benchmark]
    public QueryCommand G8_RecursiveCte_Whole_ToCommand()
    {
        Collect("rec.whole_tocommand", SampleRecursiveWholeToCommand);
        return BuildRecursiveWholeToCommand();
    }

    // ---------------------------------------------------------------------------------------------
    // Controls.
    // ---------------------------------------------------------------------------------------------

    [Benchmark]
    public long G8_Control_Noop()
    {
        Collect("control.noop", SampleNoop);
        return 0;
    }

    [Benchmark]
    public object G8_Control_Object()
    {
        Collect("control.object", SampleObject);
        return new object();
    }

    [Benchmark]
    public EntityBuilder<TableAlias> G8_Control_Builder()
    {
        Collect("control.builder", SampleBuilder);
        return _db.From("nums");
    }

    // ---------------------------------------------------------------------------------------------
    // Chain builders (verbatim oracle sequences).
    // ---------------------------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<CteNumberRow> BuildAnchor()
        => _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<CteNumberRow> BuildStep()
        => _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<CteNumberRow> BuildRecursiveBody()
    {
        var anchor = BuildAnchor();
        var step = BuildStep();
        return anchor.UnionAll(step);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<int> SelectRecursiveFinal(EntityBuilder<TableAlias> builder)
        => builder.Select(t => t["n"].AsInt);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<int> BuildRecursiveWhole()
    {
        var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        return SelectRecursiveFinal(_db.WithRecursive("nums", anchor.UnionAll(step)).From("nums"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand BuildCteBody()
        => _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CteQuery BuildCteScope()
    {
        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return _db.With("recent", recent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private EntityBuilder<TableAlias> BuildCteFrom()
        => BuildCteScope().From("recent");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JoinedEntityBuilder<TableAlias, SimpleEntity> BuildCteJoinBuilder()
        => BuildCteFrom().Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int BuildCteJoinCount() => BuildCteJoinBuilder().Joins?.Count ?? 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<CteJoinRow> SelectCteFinal(JoinedEntityBuilder<TableAlias, SimpleEntity> joined)
        => joined.Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id });

    // The oracle chain is one expression. It is decomposed into NoInlining stage helpers so that the
    // whole and every isolation execute the identical call sequence with the identical JIT/escape
    // context; the helper boundaries allocate nothing themselves.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand<CteJoinRow> BuildCteWhole() => SelectCteFinal(BuildCteJoinBuilder());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand BuildCteWholeToCommand() => BuildCteJoinBuilder().ToCommand();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private QueryCommand BuildRecursiveWholeToCommand()
    {
        var anchor = BuildAnchor();
        var step = BuildStep();
        return _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").ToCommand();
    }

    // ---------------------------------------------------------------------------------------------
    // Sample functions: every probe returns the allocated-byte delta of its measured window.
    // ---------------------------------------------------------------------------------------------

    private long SampleAnchor() => SampleBuild(BuildAnchor);

    private long SampleStep() => SampleBuild(BuildStep);

    private long SampleAnchorStep()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        _ = BuildAnchor();
        _ = BuildStep();
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    private long SampleUnionBody() => SampleBuild(BuildRecursiveBody);

    private long SampleRecursiveWhole() => SampleBuild(BuildRecursiveWhole);

    private long SampleCteBody() => SampleBuild(BuildCteBody);

    private long SampleCteBodyWith() => SampleBuild(BuildCteScope);

    private long SampleCteBodyWithFrom() => SampleBuild(BuildCteFrom);

    private long SampleCteBodyWithFromJoin() => SampleBuildInt(BuildCteJoinCount);

    private long SampleCteWhole() => SampleBuild(BuildCteWhole);

    private long SampleUnionIsolation()
    {
        var anchor = BuildAnchor();
        var step = BuildStep();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var body = anchor.UnionAll(step);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += body.UnionType == UnionType.All ? 1 : 0;
        return bytes;
    }

    private long SampleWithRecursiveIsolation()
    {
        var body = BuildRecursiveBody();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var scope = _db.WithRecursive("nums", body);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += scope.Ctes.Count;
        return bytes;
    }

    private long SampleFromIsolation()
    {
        var scope = _db.WithRecursive("nums", BuildRecursiveBody());
        var start = GC.GetAllocatedBytesForCurrentThread();
        var builder = scope.From("nums");
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += builder.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleRecursiveFinalSelectIsolation()
    {
        var builder = _db.WithRecursive("nums", BuildRecursiveBody()).From("nums");
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = SelectRecursiveFinal(builder);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleCteWithIsolation()
    {
        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        var start = GC.GetAllocatedBytesForCurrentThread();
        var scope = _db.With("recent", recent);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += scope.Ctes.Count;
        return bytes;
    }

    private long SampleCteFromIsolation()
    {
        var scope = BuildCteScope();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var builder = scope.From("recent");
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += builder.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleCteJoinIsolation()
    {
        var from = BuildCteFrom();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var joined = from.Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += joined.Joins?.Count ?? 0;
        return bytes;
    }

    private long SampleCteFinalSelectIsolation()
    {
        var joined = BuildCteJoinBuilder();
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = SelectCteFinal(joined);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleSelectCachedIsolation()
    {
        var builder = _ctx.SimpleEntity.Where(s => s.Id == 1);
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = builder.Select(CachedSelectExpr);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleSelectCachedFreshBuilder()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = _db.From<SimpleEntity>().Select(CachedSelectExpr);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleDefinition()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var definition = new QueryDefinition();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += definition.GetHashCode() < int.MinValue ? 1 : 0;
        return bytes;
    }

    private long SampleFromEntity()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var builder = _db.From<SimpleEntity>();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += builder.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleToCommandFreshBuilder()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = _db.From<SimpleEntity>().ToCommand();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleSelectInline()
    {
        var builder = _ctx.SimpleEntity.Where(s => s.Id == 1);
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = builder.Select(s => new CteNumberRow { n = s.Id });
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleAnchorToCommand()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = _ctx.SimpleEntity.Where(s => s.Id == 1).ToCommand();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleStepToCommand()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = _db.From("nums").Where(t => t["n"].AsInt < 5).ToCommand();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleBodyToCommand()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var command = _ctx.ComplexEntity.Where(c => c.Id > 1).ToCommand();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += command.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleCteWholeToCommand() => SampleBuild(BuildCteWholeToCommand);

    private long SampleRecursiveWholeToCommand() => SampleBuild(BuildRecursiveWholeToCommand);

    private long SampleNoop()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    private long SampleObject()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        _ = new object();
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    private long SampleBuilder()
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var builder = _db.From("nums");
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += builder.Ctes?.Count ?? 0;
        return bytes;
    }

    private long SampleBuild(Func<object> build)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var value = build();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += value is null ? 0 : 1;
        return bytes;
    }

    private long SampleBuildInt(Func<int> build)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var value = build();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        _sink += value;
        return bytes;
    }

    // ---------------------------------------------------------------------------------------------
    // Collector and evidence writer (never inside a measured window).
    // ---------------------------------------------------------------------------------------------

    private void Collect(string name, Func<long> sample)
    {
        lock (ResultsGate)
        {
            if (Results.ContainsKey(name)) return;

            for (var i = 0; i < WarmupSamples; i++) _sink += sample();

            var samples = new long[SampleCount];
            for (var i = 0; i < SampleCount; i++) samples[i] = sample();

            var sorted = (long[])samples.Clone();
            Array.Sort(sorted);
            Results[name] = new ProbeResult
            {
                Name = name,
                Warmup = WarmupSamples,
                Samples = SampleCount,
                Min = sorted[0],
                Median = sorted[sorted.Length / 2],
                P95 = sorted[(int)Math.Ceiling(0.95 * sorted.Length) - 1],
                Max = sorted[^1],
                SampleList = samples,
            };
            _lastProbe = name;
        }
    }

    private static ClosureCheck BuildClosure(
        Dictionary<string, ProbeResult> probes,
        string family,
        string wholeName,
        params (string Probe, string Part)[] parts)
    {
        var ledger = new Dictionary<string, long>(StringComparer.Ordinal);
        long sum = 0;
        foreach (var (probe, part) in parts)
        {
            if (!probes.TryGetValue(probe, out var result))
            {
                return new ClosureCheck
                {
                    Family = family,
                    Parts = ledger,
                    Missing = probe,
                    Pass = false,
                };
            }

            ledger[part] = result.Median;
            sum += result.Median;
        }

        if (!probes.TryGetValue(wholeName, out var whole))
        {
            return new ClosureCheck
            {
                Family = family,
                Parts = ledger,
                PartsSum = sum,
                Missing = wholeName,
                Pass = false,
            };
        }

        var tolerance = Math.Max(64L, (long)Math.Ceiling(whole.Median * 0.05));
        return new ClosureCheck
        {
            Family = family,
            Whole = whole.Median,
            PartsSum = sum,
            Error = whole.Median - sum,
            Tolerance = tolerance,
            Pass = Math.Abs(whole.Median - sum) <= tolerance,
            Parts = ledger,
        };
    }

    private static string ResolveEvidenceDir()
    {
        var custom = Environment.GetEnvironmentVariable("NEXTORM_G8_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "nextorm.slnx")))
                    return Path.Combine(dir.FullName, ".pdca", "T297", "rv1", "shape");
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), ".pdca", "T297", "rv1", "shape");
    }

    private sealed class ProbeResult
    {
        public string Name { get; init; } = "";
        public int Warmup { get; init; }
        public int Samples { get; init; }
        public long Min { get; init; }
        public long Median { get; init; }
        public long P95 { get; init; }
        public long Max { get; init; }
        public long[] SampleList { get; init; } = Array.Empty<long>();
    }

    private sealed class ClosureCheck
    {
        public string Family { get; init; } = "";
        public long Whole { get; init; }
        public long PartsSum { get; init; }
        public long Error { get; init; }
        public long Tolerance { get; init; }
        public bool Pass { get; init; }
        public string? Missing { get; init; }
        public Dictionary<string, long> Parts { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class EvidencePayload
    {
        public string Task { get; init; } = "";
        public string GeneratedUtc { get; init; } = "";
        public string Method { get; init; } = "";
        public int Warmup { get; init; }
        public int Samples { get; init; }
        public int ProbesCollected { get; init; }
        public int ProbesExpected { get; init; }
        public bool Complete { get; init; }
        public Dictionary<string, ProbeResult> Probes { get; init; } = new(StringComparer.Ordinal);
        public Dictionary<string, ClosureCheck> Closure { get; init; } = new(StringComparer.Ordinal);
    }
}
