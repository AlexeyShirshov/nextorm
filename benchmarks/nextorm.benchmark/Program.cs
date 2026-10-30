using BenchmarkDotNet.Running;
using NextORM.Benchmark;

// BenchmarkRunner.Run<ExpressionsExperiments>();
// BenchmarkRunner.Run<BenchmarkQueryCommand>();
// BenchmarkRunner.Run<InMemoryBenchmarkIteration>();
// BenchmarkRunner.Run<InMemoryBenchmarkWhere>();
// BenchmarkRunner.Run<InMemoryBenchmarkAny>();
// BenchmarkRunner.Run<SqliteBenchmarkIteration>();
// BenchmarkRunner.Run<SqliteBenchmarkLargeIteration>();
// BenchmarkRunner.Run<SqliteBenchmarkWhere>();
// BenchmarkRunner.Run<SqliteBenchmarkSimulateWork>();
// BenchmarkRunner.Run<SqliteBenchmarkMakeSelect>();
// BenchmarkRunner.Run<SqliteBenchmarkAny>();
// BenchmarkRunner.Run<SqliteBenchmarkFirst>();
// BenchmarkRunner.Run<SqliteBenchmarkSingle>();
// BenchmarkRunner.Run<SqliteBenchmarkCache>();
// BenchmarkRunner.Run<SqliteBenchmarkJoin>();

// Iteration 15 / #183 Stage A: run the non-timed diagnostic/correctness/profile batches instead of
// BenchmarkDotNet. This is a benchmark-project-only entry point; it never touches nextorm.core.
if (args.Length > 0 && args[0] == "--stage-a")
    return StageADiagnostics.Run(args.Length > 1 ? args[1] : "all");

// T217 (#217) E217-14: bounded real-ClickHouse perf probe (DTO vs DateTime reading over identical
// rows). Benchmark-project-only entry point; it never touches nextorm.core.
if (args.Length > 0 && args[0] == "--t217-perf-probe")
    return await T217DtoPerfProbe.RunAsync(args.Length > 1 ? args[1] : ".");

// T245 (#245) D245-1: allocation attribution probe (per-candidate bytes/query) for the warm
// EXCEPT/INTERSECT path. Benchmark-project-only; it never touches nextorm.core and runs no BDN job.
if (args.Length > 0 && args[0] == "--t245-attribution")
    return await T245AttributionProbe.RunAsync(
        args.Length > 1
            ? args[1]
            : Environment.GetEnvironmentVariable("NEXTORM_T245_ATTRIBUTION_OUT") ?? ".pdca/T245/rv1/attribution");

// T245 (#245) Step 1 evidence completion: measurement-only reconciliation probe. It measures the exact
// BDN arm bodies and the attribution-probe bodies in one process and against one pinned database, so the
// probe level and the acceptance arms can be compared directly. Benchmark-project-only; no product change.
if (args.Length > 0 && args[0] == "--t245-reconcile")
    return await T245ReconciliationProbe.RunAsync(
        args.Length > 1 ? args[1] : throw new ArgumentException("--t245-reconcile needs an output directory"));

// T297-G8 (#297): BenchmarkDotNet launches one process per benchmark, so the class-level static
// collector never sees more than the last probe of a BDN run. This env-gated entry point runs all
// 32 probe bodies in a single process and writes the evidence; benchmark-project-only, and with
// the flag absent this file behaves exactly as before.
if (Environment.GetEnvironmentVariable("NEXTORM_G8_PROBE_ONLY") == "1")
    return SqliteBenchmarkG8Shape.RunAllProbes();

// T333 revision r=2 (rv2): bounded, untimed correctness probe (no BDN job). Benchmark-project-only entry
// point; it never touches nextorm.core and with the flag absent this file behaves exactly as before.
if (args.Length > 0 && args[0] == "--t333-r2-probe")
    return T333R2Probe.Run(args.Length > 1 ? args[1] : throw new ArgumentException("--t333-r2-probe needs an output path"));

// T333 revision r=3 (rv3): bounded, untimed allocation probe (no BDN job). Benchmark-project-only entry
// point; it never touches nextorm.core and with the flag absent this file behaves exactly as before.
if (args.Length > 0 && args[0] == "--t333-r3-alloc")
    return T333R3AllocProbe.Run(args.Length > 1 ? args[1] : throw new ArgumentException("--t333-r3-alloc needs an output path"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;

// runner.QueryCommandPlanEqualityComparer();
// var runner = new SqliteBenchmarkSimulateWork();
// await runner.NextormPreparedToList();
// while (true)
// for (var i = 0; i < 20; i++)
//     runner.Nextorm_Cached_ToList();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();