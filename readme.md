# Next ORM
The goal is not be as fast as Dapper but get rid of manual sql code.\
All sql code and parameters should be generated automatically, with intellisense, type checking and other c# features.\
NextORM will not be truly ORM, because Object abbreviation can be surely removed - no change tracking (as inneficient), no creation entity class and mapping to it. For queries the only way to get data is projection (anonymous types, tuples, etc). 
## Features
* use only projection without mapping rdbms resultset to specific entities
* stream data (use IAsyncEnumerable). It is still posible to fetch all data into list (or any) using System.Linq.Async (ToListAsync, for example)
* ability to write custom queries without any entities and metadata at all 
## Examples
### Select data via entity to map props and type to columns and table respectively
    [SqlTable("simple_entity")]
    public interface ISimpleEntity
    {
        [Key]
        [Column("id")]
        int Id {get;set;}
    }
    //...
    // load data into anonymous object
    await foreach(var row in dataContext.SimpleEntity.Select(entity=>new {Id=entity.Id}))
    {
        _logger.LogInformation("Id = {id}", row.Id);
    }
### Select data without any entity, meta attributes, etc. Pure sql
    await foreach(var row in dataContext.From("simple_entity").Select(tbl=>new {Id=tbl.Int("id")}))
    {
        _logger.LogInformation("Id = {id}", row.Id);
    }
### Subquery with strong typings
    var innerQuery = dataContext.From("simple_entity").Select(tbl=>new {Id=tbl.Int("id")});
    await foreach(var row in dataContext.From(innerQuery).Select(subQuery=>new {subQuery.Id}))
    {
        _logger.LogInformation("Id = {id}", row.Id);
    }
    // generated code is 
    // select id from (select id from simple_entity)
