# Next ORM Entity Framework Core integration

Nextorm - high performance zero-sql object-relational mapping (ORM) library

Run nextorm reads over the connection and transaction of an existing EF Core `DbContext`, reusing
the EF model mapping instead of declaring it again.

## Installation

- from cli  
```dotnet add package nextorm.entityframeworkcore```
- from package manager  
```Install-Package nextorm.entityframeworkcore```

## Usage

```csharp
using var next = db.CreateNextOrmContext();
var rows = next.From<Order>().Select(x => new { x.Id, x.Total }).ToList();
```

## Releases

### 1.0.9-a

[Release notes](https://alexeyshirshov.github.io/nextorm/#109a)

### 1.0.8-b

[Release notes](https://alexeyshirshov.github.io/nextorm/#108b)
