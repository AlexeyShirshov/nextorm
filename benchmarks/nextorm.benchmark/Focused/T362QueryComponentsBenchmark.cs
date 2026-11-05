using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Perfolizer.Horology;

namespace NextORM.Benchmark;

/// <summary>
/// #362 AC7 paired benchmark configuration. It pins the artifacts root through
/// <see cref="BenchmarkArtifacts"/> (which honors the BDN <c>--artifacts</c> override, so a run can be
/// redirected outside the tree), keeps the in-process toolchain so the measured process is the one
/// BDN warms up, and raises the iteration count above the shared <c>NextormConfig</c> ShortRun so the
/// 99% confidence interval of the candidate/baseline ratio is conclusive instead of noise-driven.
/// </summary>
internal sealed class T362Config : ManualConfig
{
    public T362Config()
    {
        ArtifactsPath = BenchmarkArtifacts.Path;

        AddColumn(CategoriesColumn.Default);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        // Per-iteration measurements and a machine-readable full report: the AC7 ratio and its 99%
        // interval are computed from iteration data, never from a rounded summary column.
        AddExporter(CsvMeasurementsExporter.Default);
        AddExporter(JsonExporter.Full);

        AddJob(Job.Default
            .WithId("T362")
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithLaunchCount(1)
            .WithWarmupCount(6)
            .WithIterationCount(32)
            // A 200 ms floor keeps the wall clock bounded while still yielding tens of thousands of
            // invocations per iteration; the 99% interval is computed over the 32 iterations.
            .WithMinIterationTime(TimeInterval.Millisecond * 200));
    }
}

/// <summary>
/// The sixteen mandatory AC7 scenarios: Min/Max x One/All x global/grouped x scalar/composite.
/// Each scenario is measured twice, once per arm (built-in forced-portable baseline, external
/// query component), in all three phases.
/// </summary>
internal static class T362Scenarios
{
    internal static readonly ExternalExtremeKind[] Kinds = [ExternalExtremeKind.Min, ExternalExtremeKind.Max];
    internal static readonly ExtremeRowTies[] Ties = [ExtremeRowTies.One, ExtremeRowTies.All];
    internal static readonly bool[] Bools = [false, true];

    public static IEnumerable<object[]> All()
    {
        foreach (var kind in Kinds)
        {
            foreach (var ties in Ties)
            {
                foreach (var grouped in Bools)
                {
                    foreach (var composite in Bools)
                    {
                        yield return [kind, ties, grouped, composite];
                    }
                }
            }
        }
    }

    /// <summary>Stable 0..15 index of a scenario, used in place of a per-invocation dictionary lookup.</summary>
    internal static int Index(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => ((int)kind * 8) + ((int)ties * 4) + (grouped ? 2 : 0) + (composite ? 1 : 0);
}

/// <summary>
/// Builds the two arms of every scenario. Both arms attach exactly one operator to the same source
/// builder, so a measured difference is the operator's own cost, not a different query shape.
/// </summary>
internal static class T362Queries
{
    internal static EntityBuilder<ComplexEntity> Builtin(
        DataContext context,
        ExternalExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external: false);

    internal static EntityBuilder<ComplexEntity> External(
        DataContext context,
        ExternalExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external: true);

    internal static QueryCommand<ComplexEntity> Command(
        DataContext context,
        ExternalExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite,
        bool external)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external);

    /// <summary>
    /// The no-component control: an ordinary parameterized command with no query component attached.
    /// Its baseline is the same command measured on the pre-cycle tree, not the built-in operator.
    /// </summary>
    internal static EntityBuilder<ComplexEntity> Control(DataContext context)
        => context.From<ComplexEntity>().Where(e => e.Id > SqlFunctions.Parameter<int>(0));

    private static EntityBuilder<ComplexEntity> Attach(
        EntityBuilder<ComplexEntity> builder,
        ExternalExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite,
        bool external)
    {
        Expression<Func<ComplexEntity, object?>>? groupBy = grouped ? (e => e.String) : null;

        if (composite)
        {
            if (external)
            {
                return kind == ExternalExtremeKind.Min
                    ? builder.ExternalSelectWhereMin(e => new { e.Int, e.String }, ties, groupBy)
                    : builder.ExternalSelectWhereMax(e => new { e.Int, e.String }, ties, groupBy);
            }

            return kind == ExternalExtremeKind.Min
                ? builder.SelectWhereMinOld(e => new { e.Int, e.String }, ties, groupBy)
                : builder.SelectWhereMaxOld(e => new { e.Int, e.String }, ties, groupBy);
        }

        if (external)
        {
            return kind == ExternalExtremeKind.Min
                ? builder.ExternalSelectWhereMin(e => e.Int, ties, groupBy)
                : builder.ExternalSelectWhereMax(e => e.Int, ties, groupBy);
        }

        return kind == ExternalExtremeKind.Min
            ? builder.SelectWhereMinOld(e => e.Int, ties, groupBy)
            : builder.SelectWhereMaxOld(e => e.Int, ties, groupBy);
    }
}

/// <summary>
/// Phase 1: construction of the command (source builder + operator attach), with no preparation and
/// no SQL. Baseline: the built-in <c>SelectWhereMin/Max</c>; candidate: the external component.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(T362Config))]
public class T362QueryComponentsConstructionBenchmark
{
    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Construct_Builtin(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(T362Queries.Builtin(_ctx, kind, ties, grouped, composite));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Construct_External(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(T362Queries.External(_ctx, kind, ties, grouped, composite));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void Control_Construct()
        => GC.KeepAlive(T362Queries.Control(_ctx));
}

/// <summary>
/// Phase 2: first preparation of a fresh command, including SQL generation. The plan cache is not
/// filled (<c>storeInCache: false</c>) and the command is new on every invocation, so every
/// iteration pays the whole cold path. The context is shared to keep it out of the measurement.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(T362Config))]
public class T362QueryComponentsColdPrepareBenchmark
{
    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void ColdPrepare_Builtin(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T362Queries.Command(_ctx, kind, ties, grouped, composite, external: false),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void ColdPrepare_External(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T362Queries.Command(_ctx, kind, ties, grouped, composite, external: true),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void Control_ColdPrepare()
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            (QueryCommand<ComplexEntity>)T362Queries.Control(_ctx),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));
}

/// <summary>
/// Phase 3: warm cached execution of an already prepared command - plan-cache lookup (whose key
/// carries the component descriptors), parameter rebinding and row materialization. Both arms
/// prepare their commands once in <see cref="Setup"/>, so a difference is the steady-state cost, not
/// a preparation artifact. The setup run also proves each case returns rows: an empty result would
/// make the measurement vacuous.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(T362Config))]
public class T362QueryComponentsWarmBenchmark
{
    private const int ScenarioCount = 16;

    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());
    private readonly QueryCommand<ComplexEntity>[] _builtinCommands = new QueryCommand<ComplexEntity>[ScenarioCount];
    private readonly QueryCommand<ComplexEntity>[] _externalCommands = new QueryCommand<ComplexEntity>[ScenarioCount];
    private readonly IPreparedQueryCommand<ComplexEntity>[] _builtinPrepared = new IPreparedQueryCommand<ComplexEntity>[ScenarioCount];
    private readonly IPreparedQueryCommand<ComplexEntity>[] _externalPrepared = new IPreparedQueryCommand<ComplexEntity>[ScenarioCount];
    private QueryCommand<ComplexEntity> _controlCommand = null!;
    private IPreparedQueryCommand<ComplexEntity> _controlPrepared = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rows = 0;

        foreach (var kind in T362Scenarios.Kinds)
        {
            foreach (var ties in T362Scenarios.Ties)
            {
                foreach (var grouped in T362Scenarios.Bools)
                {
                    foreach (var composite in T362Scenarios.Bools)
                    {
                        var index = T362Scenarios.Index(kind, ties, grouped, composite);

                        _builtinCommands[index] = T362Queries.Command(_ctx, kind, ties, grouped, composite, external: false);
                        _builtinPrepared[index] = _ctx.GetPreparedQueryCommand(
                            _builtinCommands[index], createEnumerator: true, storeInCache: true, CancellationToken.None);

                        _externalCommands[index] = T362Queries.Command(_ctx, kind, ties, grouped, composite, external: true);
                        _externalPrepared[index] = _ctx.GetPreparedQueryCommand(
                            _externalCommands[index], createEnumerator: true, storeInCache: true, CancellationToken.None);

                        rows += _ctx.ToList(_builtinPrepared[index], default).Count;
                        rows += _ctx.ToList(_externalPrepared[index], default).Count;
                    }
                }
            }
        }

        _controlCommand = T362Queries.Control(_ctx);
        _controlPrepared = _ctx.GetPreparedQueryCommand(
            _controlCommand, createEnumerator: true, storeInCache: true, CancellationToken.None);
        rows += _ctx.ToList(_controlPrepared, new object[] { 0 }).Count;

        if (rows == 0)
            throw new InvalidOperationException("T362 benchmark setup produced no rows; the measurement would be vacuous.");

        Console.WriteLine($"T362 warm setup: {ScenarioCount} built-in + {ScenarioCount} external scenarios prepared, {rows} rows observed.");
    }

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Warm_Builtin(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => _ctx.ToList(_builtinPrepared[T362Scenarios.Index(kind, ties, grouped, composite)], default);

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExternalExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Warm_External(ExternalExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => _ctx.ToList(_externalPrepared[T362Scenarios.Index(kind, ties, grouped, composite)], default);

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void Control_Warm()
        => _ctx.ToList(_controlPrepared, new object[] { 0 });
}
