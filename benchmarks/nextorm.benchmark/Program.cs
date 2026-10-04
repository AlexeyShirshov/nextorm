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