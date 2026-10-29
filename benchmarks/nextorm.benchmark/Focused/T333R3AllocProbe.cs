using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T333 revision r=3 — bounded, untimed allocation probe (<c>--t333-r3-alloc</c>). It runs no
/// BenchmarkDotNet job: it records the exact allocated bytes per single execution of the candidate
/// <c>ConnectionLarge</c> arm (the AC05-default numerator) plus the size classes of the allocations the
/// arm is built from, so the +23 B/query residual can be attributed to a specific object instead of guessed.
/// Run it with <c>DOTNET_TieredCompilation=0</c> to match BenchmarkDotNet's generated project, which
/// disables tiered compilation: under tiered code the reported figure carries an extra tier-0 artifact.
/// </summary>
internal static class T333R3AllocProbe
{
    private const int Iterations = 100;
    private const int Batches = 20;
    private const int BatchSize = 1000;
    private const int PerCallSamples = 200;

    public static int Run(string outputPath)
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        using var db = builder.CreateDataContext();

        var repo = new TestDataRepository(db);
        ((IConnectionManager)db).EnsureConnectionOpen();

        var connectionLarge = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare();

        // Warm the plan cache, the per-connection state and its parameter map exactly like the arm's
        // GlobalSetup does, so the measured executions are steady-state.
        for (var i = 0; i < Iterations; i++)
            _ = connectionLarge.ToList(db, (long)i);

        var document = new ProbeDocument
        {
            Tree = "candidate-worktree-r3",
            DllDirectory = AppContext.BaseDirectory,
            DbPath = BenchDb.FilePath,
            ExecutionsPerBatch = BatchSize,
            Batches = Batches,
            Arm = Measure(ArmBody(connectionLarge, db), BatchSize / Iterations),
            ListDefaultCapacity = Measure(() =>
            {
                var l = new List<long>();
                l.Add(1L);
                return l.Count;
            }, BatchSize),
            ListCapacityOne = Measure(() =>
            {
                var l = new List<long>(1);
                l.Add(1L);
                return l.Count;
            }, BatchSize),
            BoxedLong = Measure(() =>
            {
                // A non-constant value so the jitter cannot fold the box away.
                object o = (long)Environment.TickCount64;
                return o.GetHashCode();
            }, BatchSize),
            CallDeltas = PerCall(ArmBody(connectionLarge, db), PerCallSamples),
            QueryDeltas = SingleCallQueryDeltas(connectionLarge, db),
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    /// <summary>One arm call: one benchmark invocation, i.e. <see cref="Iterations"/> queries.</summary>
    private static Func<int> ArmBody(IPreparedQueryCommand<long> connectionLarge, IDataContext db) => () =>
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
            n += connectionLarge.ToList(db, (long)i).Count;
        return n;
    };

    /// <summary>
    /// Runs <see cref="Batches"/> GC-isolated batches and returns the per-call allocated bytes with the
    /// sample standard deviation and the two-sided 95 % confidence half-width (t, df = batches - 1).
    /// </summary>
    private static Measurement Measure(Func<int> body, int callsPerBatch)
    {
        // Steady state first: JIT, static caches and any first-call allocation are outside the sample.
        for (var i = 0; i < 3; i++)
            _ = body();

        var samples = new double[Batches];
        for (var b = 0; b < Batches; b++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sink = 0;
            for (var c = 0; c < callsPerBatch; c++)
                sink += body();
            var after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(sink);
            samples[b] = (after - before) / (double)callsPerBatch;
        }

        var mean = samples.Average();
        var sd = Math.Sqrt(samples.Sum(x => (x - mean) * (x - mean)) / (samples.Length - 1));
        const double t = 2.093024054408263; // t(0.975, df = 19)
        return new Measurement
        {
            CallsPerBatch = callsPerBatch,
            Samples = samples,
            MeanBytesPerCall = mean,
            SampleSd = sd,
            ConfidenceHalfWidth95 = t * sd / Math.Sqrt(samples.Length),
        };
    }

    /// <summary>
    /// Allocated bytes of each of the <see cref="Iterations"/> individual queries of one warm arm call, in
    /// order, so the per-query object sizes (and therefore the chosen list capacity) are visible.
    /// </summary>
    private static long[] SingleCallQueryDeltas(IPreparedQueryCommand<long> connectionLarge, IDataContext db)
    {
        for (var warm = 0; warm < 3; warm++)
        {
            for (var i = 0; i < Iterations; i++)
                _ = connectionLarge.ToList(db, (long)i);
        }

        var deltas = new long[Iterations];
        for (var i = 0; i < Iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sink = connectionLarge.ToList(db, (long)i).Count;
            var after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(sink);
            deltas[i] = after - before;
        }

        return deltas;
    }

    /// <summary>Allocated bytes of each of <paramref name="calls"/> individual calls, in order.</summary>
    private static long[] PerCall(Func<int> body, int calls)
    {
        for (var i = 0; i < 3; i++)
            _ = body();

        var deltas = new long[calls];
        for (var c = 0; c < calls; c++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sink = body();
            var after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(sink);
            deltas[c] = after - before;
        }

        return deltas;
    }

    private sealed class ProbeDocument
    {
        public string Tree { get; set; } = "";
        public string DllDirectory { get; set; } = "";
        public string DbPath { get; set; } = "";
        public int ExecutionsPerBatch { get; set; }
        public int Batches { get; set; }
        public Measurement Arm { get; set; } = new();
        public Measurement ListDefaultCapacity { get; set; } = new();
        public Measurement ListCapacityOne { get; set; } = new();
        public Measurement BoxedLong { get; set; } = new();
        public long[] CallDeltas { get; set; } = [];
        public long[] QueryDeltas { get; set; } = [];
    }

    private sealed class Measurement
    {
        public int CallsPerBatch { get; set; }
        public double[] Samples { get; set; } = [];
        public double MeanBytesPerCall { get; set; }
        public double SampleSd { get; set; }
        public double ConfidenceHalfWidth95 { get; set; }
    }
}
