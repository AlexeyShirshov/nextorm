
using BenchmarkDotNet.Running;
using nextorm.core.benchmark;

BenchmarkRunner.Run<BenchmarkIteration>();

// var runner = new BenchmarkIteration();

// for (var i = 0; i < 2; i++)
//     await runner.Dapper();

// Console.WriteLine("Press any key to exit");
// Console.ReadKey();