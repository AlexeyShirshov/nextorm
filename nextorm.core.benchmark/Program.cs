
using BenchmarkDotNet.Running;
using nextorm.core.benchmark;

// BenchmarkRunner.Run<InMemoryBenchmarkIteration>();
// BenchmarkRunner.Run<InMemoryBenchmarkWhere>();
BenchmarkRunner.Run<SqliteBenchmarkIteration>();
//BenchmarkRunner.Run<SqliteBenchmarkLargeIteration>();
//BenchmarkRunner.Run<SqliteBenchmarkWhere>();

// var runner = new InMemoryBenchmarkWhere();

// for (var i = 0; i < 2; i++)
//     await runner.Nextorm();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();