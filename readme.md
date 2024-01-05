# Next ORM
The goal is to be as fast as Dapper but get rid of writing sql code manualy.\
All sql code and parameters should be generated automatically, with intellisense, type checking and other c# features.\
NextORM is not really an ORM, because abbreviation "Object" can be removed. There is no change tracking (as ineffective), the entity class is not required. There are many ways to map data from database to objects: declarative (via attributes), fluent api, directly in query. 
## Features
* query compilation
* query parametrization
* [ability to write queries without any entities and metadata at all](#markdown-header-select-data-without-any-entity-meta-attributes-etc-pure-sql)
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
| Method                | Nextorm |            Dapper           | EF core |
|-----------------------|:-------:|:---------------------------:|:-------:|
| Iteration             |   |                             |         |   |
| Wide object iteration |         |  |         |   |
| Where                 |         |                             |         |
| Simulate work         |         |                             |         |
| Any                   |         |                             |         |
| First                 |         |                             |         |
| Single                |         |                             |         |
