﻿
using BenchmarkDotNet.Running;
using nextorm.core.benchmark;

// BenchmarkRunner.Run<InMemoryBenchmarkIteration>();
// BenchmarkRunner.Run<InMemoryBenchmarkWhere>();
// BenchmarkRunner.Run<SqliteBenchmarkIteration>();
//BenchmarkRunner.Run<SqliteBenchmarkLargeIteration>();
BenchmarkRunner.Run<SqliteBenchmarkWhere>();
// BenchmarkRunner.Run<SqliteBenchmarkSimulateWork>();
// BenchmarkRunner.Run<SqliteBenchmarkMakeSelect>();


// var runner = new SqliteBenchmarkWhere();

// for (var i = 0; i < 2; i++)
//     await runner.NextormCached();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();