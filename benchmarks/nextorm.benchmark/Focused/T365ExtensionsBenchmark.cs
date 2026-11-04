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
using NextORM.Extensions;
using ExtremeKind = NextORM.Extensions.ExtremeKind;
using NextORM.Sqlite;
using Perfolizer.Horology;

namespace NextORM.Benchmark;

/// <summary>
/// #365 AC6 paired benchmark configuration. Mirrors T362Config: pins the artifacts root through
/// <see cref="BenchmarkArtifacts"/> (honors the BDN <c>--artifacts</c> override), in-process toolchain,
/// raised iteration count so the 99% confidence interval of the candidate/baseline ratio is conclusive.
/// </summary>
internal sealed class T365Config : ManualConfig
{
    public T365Config()
    {
        ArtifactsPath = BenchmarkArtifacts.Path;

        AddColumn(CategoriesColumn.Default);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        AddExporter(CsvMeasurementsExporter.Default);
        AddExporter(JsonExporter.Full);

        AddJob(Job.Default
            .WithId("T365")
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithLaunchCount(1)
            .WithWarmupCount(6)
            .WithIterationCount(32)
            .WithMinIterationTime(TimeInterval.Millisecond * 200));
    }
}

/// <summary>
/// The sixteen mandatory AC6 scenarios: Min/Max x One/All x global/grouped x scalar/composite.
/// </summary>
internal static class T365Scenarios
{
    internal static readonly ExtremeKind[] Kinds = [ExtremeKind.Min, ExtremeKind.Max];
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

    internal static int Index(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => ((int)kind * 8) + ((int)ties * 4) + (grouped ? 2 : 0) + (composite ? 1 : 0);
}

/// <summary>
/// Builds the two arms of every scenario. Both arms attach exactly one operator to the same source
/// builder, so a measured difference is the operator's own cost, not a different query shape.
/// </summary>
internal static class T365Queries
{
    internal static EntityBuilder<ComplexEntity> Legacy(
        DataContext context,
        ExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external: false);

    internal static EntityBuilder<ComplexEntity> Ext(
        DataContext context,
        ExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external: true);

    internal static QueryCommand<ComplexEntity> Command(
        DataContext context,
        ExtremeKind kind,
        ExtremeRowTies ties,
        bool grouped,
        bool composite,
        bool external)
        => Attach(context.From<ComplexEntity>(), kind, ties, grouped, composite, external);

    internal static EntityBuilder<ComplexEntity> Control(DataContext context)
        => context.From<ComplexEntity>().Where(e => e.Id > SqlFunctions.Parameter<int>(0));

    private static EntityBuilder<ComplexEntity> Attach(
        EntityBuilder<ComplexEntity> builder,
        ExtremeKind kind,
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
                return kind == ExtremeKind.Min
                    ? builder.SelectWhereMinEx(e => new { e.Int, e.String }, ties, groupBy)
                    : builder.SelectWhereMaxEx(e => new { e.Int, e.String }, ties, groupBy);
            }

            return kind == ExtremeKind.Min
                ? builder.SelectWhereMin(e => new { e.Int, e.String }, ties, groupBy)
                : builder.SelectWhereMax(e => new { e.Int, e.String }, ties, groupBy);
        }

        if (external)
        {
            return kind == ExtremeKind.Min
                ? builder.SelectWhereMinEx(e => e.Int, ties, groupBy)
                : builder.SelectWhereMaxEx(e => e.Int, ties, groupBy);
        }

        return kind == ExtremeKind.Min
            ? builder.SelectWhereMin(e => e.Int, ties, groupBy)
            : builder.SelectWhereMax(e => e.Int, ties, groupBy);
    }
}

[MemoryDiagnoser]
[Config(typeof(T365Config))]
public class T365ExtensionsConstructionBenchmark
{
    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Construct_Legacy(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(T365Queries.Legacy(_ctx, kind, ties, grouped, composite));

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Construct_Ext(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(T365Queries.Ext(_ctx, kind, ties, grouped, composite));

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    public void Control_Construct()
        => GC.KeepAlive(T365Queries.Control(_ctx));
}

[MemoryDiagnoser]
[Config(typeof(T365Config))]
public class T365ExtensionsColdPrepareBenchmark
{
    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void ColdPrepare_Legacy(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T365Queries.Command(_ctx, kind, ties, grouped, composite, external: false),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void ColdPrepare_Ext(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T365Queries.Command(_ctx, kind, ties, grouped, composite, external: true),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    public void Control_ColdPrepare()
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            (QueryCommand<ComplexEntity>)T365Queries.Control(_ctx),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));
}

[MemoryDiagnoser]
[Config(typeof(T365Config))]
public class T365ExtensionsWarmBenchmark
{
    private const int ScenarioCount = 16;

    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());
    private readonly QueryCommand<ComplexEntity>[] _legacyCommands = new QueryCommand<ComplexEntity>[ScenarioCount];
    private readonly QueryCommand<ComplexEntity>[] _extCommands = new QueryCommand<ComplexEntity>[ScenarioCount];
    private readonly IPreparedQueryCommand<ComplexEntity>[] _legacyPrepared = new IPreparedQueryCommand<ComplexEntity>[ScenarioCount];
    private readonly IPreparedQueryCommand<ComplexEntity>[] _extPrepared = new IPreparedQueryCommand<ComplexEntity>[ScenarioCount];
    private QueryCommand<ComplexEntity> _controlCommand = null!;
    private IPreparedQueryCommand<ComplexEntity> _controlPrepared = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rows = 0;

        foreach (var kind in T365Scenarios.Kinds)
        {
            foreach (var ties in T365Scenarios.Ties)
            {
                foreach (var grouped in T365Scenarios.Bools)
                {
                    foreach (var composite in T365Scenarios.Bools)
                    {
                        var index = T365Scenarios.Index(kind, ties, grouped, composite);

                        _legacyCommands[index] = T365Queries.Command(_ctx, kind, ties, grouped, composite, external: false);
                        _legacyPrepared[index] = _ctx.GetPreparedQueryCommand(
                            _legacyCommands[index], createEnumerator: true, storeInCache: true, CancellationToken.None);

                        _extCommands[index] = T365Queries.Command(_ctx, kind, ties, grouped, composite, external: true);
                        _extPrepared[index] = _ctx.GetPreparedQueryCommand(
                            _extCommands[index], createEnumerator: true, storeInCache: true, CancellationToken.None);

                        rows += _ctx.ToList(_legacyPrepared[index], default).Count;
                        rows += _ctx.ToList(_extPrepared[index], default).Count;
                    }
                }
            }
        }

        _controlCommand = T365Queries.Control(_ctx);
        _controlPrepared = _ctx.GetPreparedQueryCommand(
            _controlCommand, createEnumerator: true, storeInCache: true, CancellationToken.None);
        rows += _ctx.ToList(_controlPrepared, new object[] { 0 }).Count;

        if (rows == 0)
            throw new InvalidOperationException("T365 benchmark setup produced no rows; the measurement would be vacuous.");

        Console.WriteLine($"T365 warm setup: {ScenarioCount} legacy + {ScenarioCount} ext scenarios prepared, {rows} rows observed.");
    }

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Warm_Legacy(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => _ctx.ToList(_legacyPrepared[T365Scenarios.Index(kind, ties, grouped, composite)], default);

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Min, ExtremeRowTies.All, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.One, true, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, false, true)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, false)]
    [Arguments(ExtremeKind.Max, ExtremeRowTies.All, true, true)]
    public void Warm_Ext(ExtremeKind kind, ExtremeRowTies ties, bool grouped, bool composite)
        => _ctx.ToList(_extPrepared[T365Scenarios.Index(kind, ties, grouped, composite)], default);

    [Benchmark]
    [BenchmarkCategory("T365", "query-components")]
    public void Control_Warm()
        => _ctx.ToList(_controlPrepared, new object[] { 0 });
}