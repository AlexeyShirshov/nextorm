# Tiny orm
The goal is not be as fast as Dapper but get rid of manual sql code.
All sql code and parameters should be generated automatically, with intellisense, type checking and other c# features.
## Features
* use only projection without mapping rdbms resultset to specific entities
* stream data (use IAsyncEnumerable)
* ability to write custom queries without any mappings at all 
## Examples
### Select data via mapping entity
'''cs
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id {get;set;}
}
//...
// load data to anonymous object
await foreach(var row in dataContext.SimpleEntity.Select(entity=>new {Id=entity.Id}))
{
    _logger.LogInformation("Id = {id}", row.Id);
}
'''
### Select data without any mapping
    await foreach(var row in dataContext.From("simple_entity").Select(tbl=>new {Id=tbl.Int("id")}))
    {
        _logger.LogInformation("Id = {id}", row.Id);
    }