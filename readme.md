# Next ORM

The goal is to be as fast as Dapper but get rid of writing sql code manually.\
All sql code and parameters should be generated automatically, with intellisense, type checking and other c# features.\
NextORM is not really an ORM, because abbreviation "Object" can be removed. There is no change tracking (as ineffective), the entity class is not required. There are many ways to map data from database to objects: declarative (via attributes), fluent api, directly in query.

## Features

* query compilation
* query parametrization
* [ability to write queries without any entities and metadata at all](#select-data-without-any-entity-meta-attributes-etc-pure-sql)

## Examples

### Select data via entity to map props and type to columns and table respectively

``` csharp
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id {get;set;}
}
//...
// load data into anonymous object
foreach(var row in await dataContext.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
{
    _logger.LogInformation("Id = {id}", row.Id);
}
```

### Select data without any entity, meta attributes, etc. Pure sql

``` csharp
foreach(var row in await dataContext.From("simple_entity").Select(tbl => new { Id = tbl.Int("id") }).ToListAsync())
{
    _logger.LogInformation("Id = {id}", row.Id);
}
```

### Subquery with strong typings

``` csharp
var innerQuery = dataContext.From("simple_entity").Select(tbl => new { Id = tbl.Int("id") });
await foreach(var row in dataContext.From(innerQuery).Select(subQuery=>new { subQuery.Id }))
{
    _logger.LogInformation("Id = {id}", row.Id);
}
// generated code is 
// select id from (select id from simple_entity)
```

## Benchmarks

Benchmark is running under the following conditions:

* Nextorm is compiled buffered
* Dapper is buffered
* EF core is compiled buffered
* All queries is async
* Data provider is SQLite
* Computer configuration: Intel(R) Core(TM) i5-9600KF CPU @ 3.70GHz, RAM 16Gb

| Method                | Nextorm |  Dapper | EF core |
|-----------------------|:-------:|:---------------------------:|:-------:|
| [Data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkIteration-report-github.md)             | 41.08 μs | 42.28 μs | 57.95 us |   |
| [Wide data fetch](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkLargeIteration-report-github.md) | 10.060 ms | 13.789 ms | 12.858 ms |   |
| [Where](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkWhere-report-github.md)                 | 4.002 ms | 4.208 ms | 5.442 ms |
| [Simulate work](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSimulateWork-report-github.md)         | 182.4 ms | 201.9 ms | 292.3 ms |
| [Any](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkAny-report-github.md)                   | 31.74 us | 38.83 us | 54.16 us |
| [FirstOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkFirst-report-github.md)                 | 337.4 us | 432.2 us | 586.9 us |
| [SingleOrDefault](https://github.com/AlexeyShirshov/nextorm/blob/main/nextorm.benchmark/BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkSingle-report-github.md)                | 36.65 us | 39.60 us | 51.99 us |

Summary

| Place                | Nextorm |  Dapper | EF core |
|-----------------------|:-------:|:---------------------------:|:-------:|
| First | 7 | | |
| Second | | 6 | 1 |
| Third | | 1 | 6 |
