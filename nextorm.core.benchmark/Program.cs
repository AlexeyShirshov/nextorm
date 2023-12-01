using BenchmarkDotNet.Running;
using nextorm.core.benchmark;

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

// runner.QueryCommandPlanEqualityComparer();
var runner = new InMemoryBenchmarkAny();
// await runner.NextormCompiledToList();
// while (true)
for (var i = 0; i < 10; i++)
    runner.NextormCompiled();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();