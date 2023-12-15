using BenchmarkDotNet.Running;
using nextorm.benchmark;

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
BenchmarkRunner.Run<SqliteBenchmarkFirst>();
// BenchmarkRunner.Run<SqliteBenchmarkSingle>();

// runner.QueryCommandPlanEqualityComparer();
// var runner = new SqliteBenchmarkIteration(true);
// await runner.NextormCompiledToList();
// while (true)
// for (var i = 0; i < 2; i++)
// runner.CompileQueries();
// await runner.NextormCompiledToList();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();