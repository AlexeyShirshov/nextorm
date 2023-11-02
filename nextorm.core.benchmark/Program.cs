
using BenchmarkDotNet.Running;
using nextorm.core.benchmark;

//BenchmarkRunner.Run<InMemoryBenchmarkIteration>();
BenchmarkRunner.Run<SqliteBenchmarkIteration>();
//BenchmarkRunner.Run<SqliteBenchmarkLargeIteration>();

// var runner = new InMemoryBenchmarkIteration();

// for (var i = 0; i < 2; i++)
//     await runner.Nextorm();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();