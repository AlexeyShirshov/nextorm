﻿using nextorm.core.benchmark;

// BenchmarkRunner.Run<ExpressionsExperiments>();
// BenchmarkRunner.Run<InMemoryBenchmarkIteration>();
// BenchmarkRunner.Run<InMemoryBenchmarkWhere>();
// BenchmarkRunner.Run<SqliteBenchmarkIteration>();
// BenchmarkRunner.Run<SqliteBenchmarkLargeIteration>();
// BenchmarkRunner.Run<SqliteBenchmarkWhere>();
// BenchmarkRunner.Run<SqliteBenchmarkSimulateWork>();
// BenchmarkRunner.Run<SqliteBenchmarkMakeSelect>();

var runner = new SqliteBenchmarkIteration(true);

for (var i = 0; i < 1; i++)
    await runner.EFCoreAny();

//await runner.FillLargeTable();
// Console.WriteLine("Press any key to exit");
// Console.ReadKey();