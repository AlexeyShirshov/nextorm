using System.Diagnostics;
using System.Linq.Expressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

[Collection("Query plan cache")]
public sealed class Ac6EvidenceTests
{
    private sealed class Row
    {
        public long Id { get; set; }
        public int? Int { get; set; }
        public string? String { get; set; }
    }

    private sealed record Scenario(string Name, ExtremeKind Kind, ExtremeRowTies Ties, bool Grouped, bool Composite);
    private sealed record Sample(string Scenario, int Pair, double Ratio);
    private sealed record Summary(string Scenario, double Mean, double Low, double High, double AllocEx, double AllocLegacy, string Verdict, int WarmBuildCalls = 0, bool ResultEqual = true);
    private sealed class BuildCounter : IQueryComponent
    {
        private readonly IQueryComponent _inner;
        public int Count;
        public BuildCounter(IQueryComponent inner) => _inner = inner;
        public Type Slot => _inner.Slot;
        public QueryComponentContribution Contribution => _inner.Contribution;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) => _inner.Prepare(context);
        public void BuildQuery(IQueryComponentBuildContext context) { Count++; _inner.BuildQuery(context); }
    }

    [Fact]
    public void ProduceAc6Evidence()
    {
        if (Environment.GetEnvironmentVariable("T365_EVIDENCE") != "1")
            Assert.Skip("Set T365_EVIDENCE=1 to run the T365 evidence harness.");

        var root = Environment.GetEnvironmentVariable("T365_EVIDENCE_DIR") ?? ".pdca/365-366-r2/items/T365/rv1";
        if (!Path.IsPathRooted(root)) root = Path.GetFullPath(root, FindRepositoryRoot());
        var sqlDir = Path.Combine(root, "sql-identity");
        var measureDir = Path.Combine(root, "measure");
        Directory.CreateDirectory(sqlDir);
        Directory.CreateDirectory(measureDir);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var ddl = connection.CreateCommand())
        {
            ddl.CommandText = "CREATE TABLE Row (Id INTEGER PRIMARY KEY, Int INTEGER NULL, String TEXT NULL); INSERT INTO Row VALUES (1,1,'a'),(2,1,'a'),(3,2,'b'),(4,NULL,NULL),(5,3,'c');";
            ddl.ExecuteNonQuery();
        }
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        context.PurgeQueryCache();

        var all = new[]
        {
            new Scenario("Max-All-global-scalar", ExtremeKind.Max, ExtremeRowTies.All, false, false),
            new Scenario("Max-All-global-composite", ExtremeKind.Max, ExtremeRowTies.All, false, true),
            new Scenario("Max-All-grouped-scalar", ExtremeKind.Max, ExtremeRowTies.All, true, false),
            new Scenario("Min-All-global-scalar", ExtremeKind.Min, ExtremeRowTies.All, false, false),
            new Scenario("Min-All-global-composite", ExtremeKind.Min, ExtremeRowTies.All, false, true),
            new Scenario("Min-All-grouped-scalar", ExtremeKind.Min, ExtremeRowTies.All, true, false)
        };
        var identity = new List<string>();
        foreach (var scenario in all)
        {
            var legacy = Prepared(context, scenario, false, out _, out _);
            var external = Prepared(context, scenario, true, out _, out _);
            var legacySql = ((DbPreparedQueryCommand<Row>)legacy).SqlStmt ?? string.Empty;
            var externalSql = ((DbPreparedQueryCommand<Row>)external).SqlStmt ?? string.Empty;
            var legacyText = legacySql + "\n-- parameters: " + ParameterNames(legacy);
            var externalText = externalSql + "\n-- parameters: " + ParameterNames(external);
            File.WriteAllText(Path.Combine(sqlDir, scenario.Name + "-legacy.sql"), legacyText, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(sqlDir, scenario.Name + "-ex.sql"), externalText, new UTF8Encoding(false));
            var equal = legacyText == externalText;
            File.WriteAllText(Path.Combine(sqlDir, scenario.Name + ".diff"), UnifiedDiff(legacyText, externalText, scenario.Name), new UTF8Encoding(false));
            identity.Add($"{scenario.Name}: SqlStmt={legacySql == externalSql}; parameterNames={ParameterNames(legacy) == ParameterNames(external)}; combined={equal}");
        }
        File.WriteAllLines(Path.Combine(sqlDir, "verdicts.txt"), identity, new UTF8Encoding(false));

        var scenarios = new List<Scenario>();
        foreach (var kind in new[] { ExtremeKind.Min, ExtremeKind.Max })
        foreach (var ties in new[] { ExtremeRowTies.One, ExtremeRowTies.All })
        foreach (var grouped in new[] { false, true })
        foreach (var composite in new[] { false, true })
            scenarios.Add(new Scenario($"{kind}-{ties}-{(grouped ? "grouped" : "global")}-{(composite ? "composite" : "scalar")}", kind, ties, grouped, composite));

        var diagnostics = new List<string>();

        const int pairs = 400;
        const int k = 64;
        var random = new Random(365);
        var samples = new List<Sample>();
        var summaries = new List<Summary>();
        foreach (var scenario in scenarios)
        {
            var legacy = Prepared(context, scenario, false, out _, out var legacySource);
            var legacyAgain = context.GetPreparedQueryCommand(legacySource, false, true, CancellationToken.None);
            legacyAgain.Should().BeSameAs(legacy, $"{scenario.Name}: second legacy preparation must be a plan-cache hit");
            var external = Prepared(context, scenario, true, out var extCounter, out _);
            var diagnoseScenario = Environment.GetEnvironmentVariable("T365_DIAG") == "1" && scenario.Name is "Min-All-grouped-composite" or "Max-All-grouped-composite";
            if (Environment.GetEnvironmentVariable("T365_DIAG") == "1" && scenario.Name is "Min-All-grouped-composite" or "Max-All-grouped-composite")
            {
                diagnostics.Add($"{scenario.Name}\nLegacy rows: {FormatRows(Ids(context, legacy))}\nEx rows: {FormatRows(Ids(context, external))}");
            }
            var legacyIds = Ids(context, legacy);
            var extIds = Ids(context, external);
            legacyIds.Should().Equal(extIds, $"{scenario.Name} must match before timing");
            var coldBuildCount = extCounter!.Count;
            for (var i = 0; i < 5; i++) { Execute(context, legacy); Execute(context, external); }
            var warmBuilds = extCounter.Count - coldBuildCount;
            warmBuilds.Should().Be(0, $"{scenario.Name} must be a confirmed warm cache hit");
            for (var i = 0; i < 100; i++) Execute(context, external);
            (extCounter.Count - coldBuildCount).Should().Be(0, $"{scenario.Name}: BuildQuery ran during 100 warm executions");
            diagnostics.Add($"Warm hit {scenario.Name}: Ex BuildQuery delta={extCounter.Count-coldBuildCount}; legacy prepared instance reused={ReferenceEquals(legacy, legacyAgain)}");
            if (diagnoseScenario)
            {
                for (var i = 0; i < 100; i++) Execute(context, legacy);
            diagnostics.Add($"BuildQuery calls over 100 executions: legacy=0 (no component), Ex={extCounter.Count - coldBuildCount}");
            }
            var ratios = new double[pairs];
            for (var pair = 0; pair < pairs; pair++)
            {
                var rotate = (pair & 1) != 0;
                long l1, l2, e1, e2;
                if (!rotate)
                {
                    l1 = TimeBlock(context, legacy, k); e1 = TimeBlock(context, external, k);
                    e2 = TimeBlock(context, external, k); l2 = TimeBlock(context, legacy, k);
                }
                else
                {
                    e1 = TimeBlock(context, external, k); l1 = TimeBlock(context, legacy, k);
                    l2 = TimeBlock(context, legacy, k); e2 = TimeBlock(context, external, k);
                }
                if (diagnoseScenario && pair < 5)
                {
                    var durations = new[] { l1, e1, e2, l2 };
                    var labels = new[] { "A(legacy)", "B(Ex)", "B(Ex)", "A(legacy)" };
                    var allocs = new[] { Allocations(context, legacy, k), Allocations(context, external, k), Allocations(context, external, k), Allocations(context, legacy, k) };
                    diagnostics.Add($"Pair {pair + 1} durations (µs): " + string.Join(", ", labels.Select((label, index) => $"{label}={ToMicroseconds(durations[index]):R}")));
                    diagnostics.Add($"Pair {pair + 1} allocated bytes/block: " + string.Join(", ", labels.Select((label, index) => $"{label}={allocs[index] * k:R}")));
                }
                ratios[pair] = (double)(e1 + e2) / (l1 + l2);
                samples.Add(new Sample(scenario.Name, pair, ratios[pair]));
            }
            var allocLegacy = Allocations(context, legacy, k);
            var allocEx = Allocations(context, external, k);
            var (low, high) = Bootstrap(ratios, random);
            summaries.Add(new Summary(scenario.Name, ratios.Average(), low, high, allocEx, allocLegacy, high <= 1 && allocEx <= allocLegacy ? "met" : "unmet", warmBuilds));
        }
        var control = Control(context);
        using var plainConnection = new SqliteConnection("Data Source=:memory:");
        plainConnection.Open();
        using (var ddl = plainConnection.CreateCommand()) { ddl.CommandText = "CREATE TABLE Row (Id INTEGER PRIMARY KEY, Int INTEGER NULL, String TEXT NULL); INSERT INTO Row VALUES (1,1,'a'),(2,1,'a'),(3,2,'b'),(4,NULL,NULL),(5,3,'c');"; ddl.ExecuteNonQuery(); }
        using var plainContext = new SqliteDataContext(plainConnection, new DataContextBuilder());
        plainContext.PurgeQueryCache();
        var plainControl = Control(plainContext);
        var controlIds = Ids(context, control); var plainIds = Ids(plainContext, plainControl);
        controlIds.Should().Equal(plainIds);
        for (var i = 0; i < 5; i++) { Execute(context, control); Execute(plainContext, plainControl); }
        var controlSamples = MeasureCrossContext(context, plainContext, "no-component-control", control, plainControl, pairs, k, random, samples) with { ResultEqual = controlIds.SequenceEqual(plainIds), Verdict = $"control BuildCounter=0; rows={controlIds.Length}" };
        summaries.Add(controlSamples);
        diagnostics.Add($"No-component control: same results={controlIds.SequenceEqual(plainIds)}; rows={controlIds.Length}/{plainIds.Length}; BuildCounter=0; CI99={controlSamples.Low:R}..{controlSamples.High:R}");
        foreach (var scenario in all)
        {
            var legacy = Prepared(context, scenario, false, out _, out _);
            var legacy2 = Prepared(context, scenario, false, out _, out _);
            Ids(context, legacy).Should().Equal(Ids(context, legacy2), $"A/A {scenario.Name} result sets must match");
            // Both A arms are prepared above; warm symmetrically in discarded alternating blocks.
            for (var i = 0; i < 20; i++)
            {
                Execute(context, legacy);
                Execute(context, legacy2);
            }
            var aa = MeasurePair(context, "A/A-" + scenario.Name, legacy, legacy2, pairs, k, random, samples);
            var containsOne = aa.Low <= 1.0 && 1.0 <= aa.High;
            diagnostics.Add($"A/A {scenario.Name}: CI99={aa.Low:R}..{aa.High:R}; 1.0 inside={containsOne}" + (containsOne ? "" : "; PROTOCOL DEFECT"));
            summaries.Add(aa with { Verdict = containsOne ? "unverified-A/A-calibrated" : "unverified-A/A-protocol-defect" });
        }
        File.WriteAllText(Path.Combine(measureDir, "samples.csv"), "scenario,pair,ratio\n" + string.Join("\n", samples.Select(s => $"{s.Scenario},{s.Pair},{s.Ratio.ToString("R", CultureInfo.InvariantCulture)}")) + "\n", new UTF8Encoding(false));
        var header = "scenario,result_set_equal,warm_build_calls,mean_ratio,ci99_lower,ci99_upper,alloc_ex,alloc_legacy,verdict\n";
        File.WriteAllText(Path.Combine(measureDir, "summary.csv"), header + string.Join("\n", summaries.Select(s => $"{s.Scenario},{s.ResultEqual},{s.WarmBuildCalls},{s.Mean:R},{s.Low:R},{s.High:R},{s.AllocEx:R},{s.AllocLegacy:R},{s.Verdict}")) + "\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(measureDir, "summary.md"), "| Scenario | Results equal | Warm BuildQuery calls | Mean ratio | CI99 lower | CI99 upper | Alloc ex | Alloc legacy | Verdict |\n|---|---:|---:|---:|---:|---:|---:|---:|---|\n" + string.Join("\n", summaries.Select(s => $"| {s.Scenario} | {s.ResultEqual} | {s.WarmBuildCalls} | {s.Mean:R} | {s.Low:R} | {s.High:R} | {s.AllocEx:R} | {s.AllocLegacy:R} | {s.Verdict} |")) + "\n", new UTF8Encoding(false));
        if (Environment.GetEnvironmentVariable("T365_DIAG") == "1")
        {
            var diag = string.Join("\n", diagnostics) + "\n";
            File.WriteAllText(Path.Combine(measureDir, "diag.txt"), diag, new UTF8Encoding(false));
            Console.Write(diag);
        }
        summaries.Count.Should().Be(23);
    }

    private static IPreparedQueryCommand<Row> Prepared(SqliteDataContext context, Scenario s, bool ex, out BuildCounter? counter, out QueryCommand<Row> source)
    {
        var builder = context.From<Row>();
        Expression<Func<Row, object?>>? group = s.Grouped ? x => x.String : null;
        var component = new ExtremeRowComponent<Row>(s.Kind, s.Composite ? (Expression<Func<Row, object?>>)(x => new { x.Int, x.String }) : x => x.Int, s.Ties, group);
        counter = ex ? new BuildCounter(component) : null;
        QueryCommand<Row> command;
        if (ex)
        {
            builder = (EntityBuilder<Row>)builder.WithComponent(counter!);
            command = (QueryCommand<Row>)builder;
        }
        else if (s.Composite)
        {
            command = (QueryCommand<Row>)(s.Kind == ExtremeKind.Min
                ? builder.SelectWhereMin(x => new { x.Int, x.String }, s.Ties, group)
                : builder.SelectWhereMax(x => new { x.Int, x.String }, s.Ties, group));
        }
        else
        {
            command = (QueryCommand<Row>)(s.Kind == ExtremeKind.Min
                ? builder.SelectWhereMin(x => x.Int, s.Ties, group)
                : builder.SelectWhereMax(x => x.Int, s.Ties, group));
        }
        source = command;
        return context.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
    }

    private static string ParameterNames(IPreparedQueryCommand<Row> command)
    {
        var value = typeof(DbPreparedQueryCommand<Row>).GetProperty("DbCommandParams", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(command);
        return string.Join(",", ((System.Collections.IEnumerable)value!).Cast<object>().Select(p => p.GetType().GetProperty("ParameterName")?.GetValue(p)?.ToString() ?? p.ToString()));
    }
    // Structural no-component proof: public From<TEntity>().Where(...) creates an ordinary query;
    // unlike EntityBuilder.WithComponent(...), this chain attaches no query component.
    private static IPreparedQueryCommand<Row> Control(SqliteDataContext c)
        => c.GetPreparedQueryCommand((QueryCommand<Row>)c.From<Row>().Where(x => x.Id > 0), false, true, CancellationToken.None);
    private static void Execute(SqliteDataContext c, IPreparedQueryCommand<Row> p) => _ = c.ToList(p, default);
    private static long TimeBlock(SqliteDataContext c, IPreparedQueryCommand<Row> p, int count)
    {
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < count; i++) Execute(c, p);
        return Stopwatch.GetTimestamp() - start;
    }
    private static double Allocations(SqliteDataContext c, IPreparedQueryCommand<Row> p, int count)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) Execute(c, p);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)count;
    }
    private static Summary MeasurePair(SqliteDataContext c, string name, IPreparedQueryCommand<Row> legacy, IPreparedQueryCommand<Row> ex, int pairs, int k, Random random, List<Sample> samples)
    {
        var ratios = new double[pairs];
        for (var i = 0; i < pairs; i++)
        {
            var rotate = (i & 1) != 0;
            long l1, l2, e1, e2;
            if (!rotate) { l1 = TimeBlock(c, legacy, k); e1 = TimeBlock(c, ex, k); e2 = TimeBlock(c, ex, k); l2 = TimeBlock(c, legacy, k); }
            else { e1 = TimeBlock(c, ex, k); l1 = TimeBlock(c, legacy, k); l2 = TimeBlock(c, legacy, k); e2 = TimeBlock(c, ex, k); }
            ratios[i] = (double)(e1 + e2) / (l1 + l2); samples.Add(new Sample(name, i, ratios[i]));
        }
        var allocLegacy = Allocations(c, legacy, k); var allocEx = Allocations(c, ex, k);
        var (low, high) = Bootstrap(ratios, random);
        return new Summary(name, ratios.Average(), low, high, allocEx, allocLegacy, "unverified-control");
    }
    private static Summary MeasureCrossContext(SqliteDataContext aContext, SqliteDataContext bContext, string name, IPreparedQueryCommand<Row> a, IPreparedQueryCommand<Row> b, int pairs, int k, Random random, List<Sample> samples)
    {
        var ratios = new double[pairs];
        for (var i = 0; i < pairs; i++) { var ta = TimeBlock(aContext, a, k); var tb = TimeBlock(bContext, b, k); ratios[i] = (double)tb / ta; samples.Add(new Sample(name, i, ratios[i])); }
        var allocA = Allocations(aContext, a, k); var allocB = Allocations(bContext, b, k); var (low, high) = Bootstrap(ratios, random);
        return new Summary(name, ratios.Average(), low, high, allocB, allocA, "unverified");
    }
    private static long[] Ids(SqliteDataContext c, IPreparedQueryCommand<Row> prepared) => c.ToList(prepared, default).Select(r => r.Id).ToArray();
    private static string FormatRows(long[] ids) => $"count={ids.Length}, ordered_id_hash={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", ids))))}";
    private static double ToMicroseconds(long ticks) => ticks * 1_000_000d / Stopwatch.Frequency;
    private static (double Low, double High) Bootstrap(double[] values, Random random)
    {
        var estimates = new double[20_000];
        var sample = new double[values.Length];
        for (var b = 0; b < estimates.Length; b++) { for (var i = 0; i < sample.Length; i++) sample[i] = values[random.Next(values.Length)]; estimates[b] = sample.Average(); }
        Array.Sort(estimates);
        return (estimates[199], estimates[19_799]);
    }
    private static string UnifiedDiff(string oldText, string newText, string name)
    {
        var oldLines = oldText.Split('\n'); var newLines = newText.Split('\n');
        if (oldText == newText) return $"--- {name}-legacy\n+++ {name}-ex\n (identical)\n";
        var sb = new StringBuilder($"--- {name}-legacy\n+++ {name}-ex\n@@ legacy vs ex @@\n");
        foreach (var line in oldLines) sb.Append('-').AppendLine(line);
        foreach (var line in newLines) sb.Append('+').AppendLine(line);
        return sb.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nextorm.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root for evidence output.");
    }
}
