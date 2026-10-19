using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NextORM.Benchmark;
using NextORM.Core.Probe;

namespace T297Probe;

/// <summary>
/// T297 r=4 P02 attribution harness. Hosts the exact benchmark class from the tree under test and runs
/// the four cached arms with the phase probe enabled. One process per tree (assembly names collide).
/// </summary>
internal static class ProbeProgram
{
    private const int QueriesPerInvocation = 10; // SqliteBenchmarkFeaturesFairCached.Iterations

    private static readonly Dictionary<string, Func<SqliteBenchmarkFeaturesFairCached, Task>> Arms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cte"] = b => b.B_Nextorm_Cached_Cte(),
            ["recursive"] = b => b.B_Nextorm_Cached_RecursiveCte(),
            ["inlist"] = b => b.B_Nextorm_Cached_In_ListContains_Captured(),
            ["leftjoin"] = b => b.B_Nextorm_Cached_LeftJoin(),
        };

    private static readonly FieldInfo SinkField =
        typeof(SqliteBenchmarkFeaturesFairCached).GetField("_sink", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SqliteBenchmarkFeaturesFairCached._sink not found");

    public static async Task<int> Main(string[] args)
    {
        if (!TryParse(args, out var arm, out var warmup, out var iters, out var overheadN, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("usage: <probe> [--arm cte|recursive|inlist|leftjoin|all] [--warmup N] [--iters N] [--overhead-n N]");
            return 2;
        }

        var selected = arm.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? new List<string> { "cte", "recursive", "inlist", "leftjoin" }
            : new List<string> { arm.ToLowerInvariant() };

        var bench = new SqliteBenchmarkFeaturesFairCached();

        // Keep stdout pure JSON: the verification report goes to stderr.
        var stdout = Console.Out;
        Console.SetOut(Console.Error);
        try
        {
            bench.Verify();
        }
        finally
        {
            Console.SetOut(stdout);
        }

        var overhead = MeasureOverhead(overheadN);
        Console.WriteLine(Json(new Dictionary<string, object?>
        {
            ["kind"] = "overhead",
            ["n"] = overheadN,
            ["pair_ns"] = overhead,
            ["frequency"] = T297PhaseProbe.Frequency,
        }));
        Console.Out.Flush();

        foreach (var name in selected)
        {
            var invoke = Arms[name];

            for (var i = 0; i < warmup; i++)
                await invoke(bench).ConfigureAwait(false);

            T297PhaseProbe.Reset();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            var allocThreadBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < iters; i++)
                await invoke(bench).ConfigureAwait(false);
            var end = Stopwatch.GetTimestamp();
            var allocThreadAfter = GC.GetAllocatedBytesForCurrentThread();
            var allocAfter = GC.GetTotalAllocatedBytes(precise: true);

            var snapshot = T297PhaseProbe.TakeSnapshot();
            var queries = iters * QueriesPerInvocation;
            var ticks = new Dictionary<string, object?>();
            var calls = new Dictionary<string, object?>();
            for (var p = 0; p < T297PhaseProbe.PhaseCount; p++)
            {
                ticks[T297PhaseProbe.PhaseNames[p]] = snapshot.Ticks[p];
                calls[T297PhaseProbe.PhaseNames[p]] = snapshot.Calls[p];
            }

            var totalAlloc = allocAfter - allocBefore;
            var threadAlloc = allocThreadAfter - allocThreadBefore;
            Console.WriteLine(Json(new Dictionary<string, object?>
            {
                ["arm"] = name,
                ["iters"] = iters,
                ["queries"] = queries,
                ["frequency"] = T297PhaseProbe.Frequency,
                ["ticks"] = ticks,
                ["calls"] = calls,
                ["alloc"] = new Dictionary<string, object?>
                {
                    ["total_per_arm"] = totalAlloc,
                    ["per_query"] = queries == 0 ? 0 : totalAlloc / (double)queries,
                    ["current_thread"] = threadAlloc,
                    ["construct"] = null,
                    ["prepare"] = null,
                    ["execute"] = null,
                },
                ["end_to_end_ticks"] = end - start,
                ["sink"] = SinkField.GetValue(bench),
            }));
            Console.Out.Flush();
        }

        return 0;
    }

    private static double MeasureOverhead(int n)
    {
        for (var i = 0; i < 1000; i++)
        {
            var t = T297PhaseProbe.Tick();
            T297PhaseProbe.Add(T297PhaseProbe.Construct, t);
        }

        T297PhaseProbe.Reset();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
        {
            var t = T297PhaseProbe.Tick();
            T297PhaseProbe.Add(T297PhaseProbe.Construct, t);
        }

        sw.Stop();
        T297PhaseProbe.Reset();
        return sw.Elapsed.TotalNanoseconds / n;
    }

    private static bool TryParse(
        string[] args,
        out string arm,
        out int warmup,
        out int iters,
        out int overheadN,
        out string error)
    {
        arm = "all";
        warmup = 3;
        iters = 3;
        overheadN = 100_000;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                error = "help";
                return false;
            }

            if (i + 1 >= args.Length)
            {
                error = "missing value for " + arg;
                return false;
            }

            var value = args[++i];
            switch (arg)
            {
                case "--arm":
                    arm = value;
                    break;
                case "--warmup":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out warmup))
                    {
                        error = "invalid --warmup: " + value;
                        return false;
                    }
                    break;
                case "--iters":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iters))
                    {
                        error = "invalid --iters: " + value;
                        return false;
                    }
                    break;
                case "--overhead-n":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out overheadN))
                    {
                        error = "invalid --overhead-n: " + value;
                        return false;
                    }
                    break;
                default:
                    error = "unknown argument: " + arg;
                    return false;
            }
        }

        if (!arm.Equals("all", StringComparison.OrdinalIgnoreCase) && !Arms.ContainsKey(arm))
        {
            error = "unknown arm: " + arm;
            return false;
        }

        if (warmup < 0 || iters < 1 || overheadN < 1)
        {
            error = "warmup must be >= 0, iters and overhead-n must be >= 1";
            return false;
        }

        return true;
    }

    private static string Json(Dictionary<string, object?> values)
    {
        var sb = new StringBuilder(512);
        sb.Append('{');
        var first = true;
        foreach (var (key, value) in values)
        {
            if (!first)
                sb.Append(',');
            first = false;
            AppendValue(sb, key, value);
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string key, object? value)
    {
        sb.Append('"').Append(key).Append("\":");
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case string s:
                AppendString(sb, s);
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                break;
            case Dictionary<string, object?> map:
                sb.Append('{');
                var first = true;
                foreach (var (k, v) in map)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    AppendValue(sb, k, v);
                }
                sb.Append('}');
                break;
            default:
                AppendString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }
}
