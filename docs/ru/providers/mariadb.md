# Провайдер MariaDB

> Используйте `nextorm.mariadb` для MariaDB 10.4+; он переиспользует отрисовку MySQL и добавляет варианты операций над множествами `INTERSECT ALL`/`EXCEPT ALL`.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [MySQL](mysql.md)

## Обзор

`MariaDbContext` (`src/nextorm.mariadb/MariaDbContext.cs`) наследуется от `MySqlDbContext`, поэтому
использует тот же драйвер `MySqlConnector` и то же управление соединением и параметрами. Он
возвращает `MariaDbDialect.Instance` из свойства `Dialect`.

`MariaDbDialect` (`src/nextorm.mariadb/MariaDbDialect.cs`) наследуется от `MySqlDialect` и меняет одну
возможность: `INTERSECT ALL` и `EXCEPT ALL` поддерживаются в MariaDB 10.4 и новее, поэтому
`SupportsIntersectExceptAll` равно `true`. Всё остальное (параметры, квотирование, `concat`,
coalesce, разбиение на страницы, имена агрегатов) наследуется без изменений.

## Регистрация провайдера

На `DbContextBuilder` доступны две перегрузки
(`src/nextorm.mariadb/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.mariadb;

var builder = new DbContextBuilder()
    .UseMariaDb("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

using var ctx = builder.CreateDbContext();   // IDataContext
```

Также можно создать контекст напрямую:

```csharp
using nextorm.core;
using nextorm.mariadb;

using IDataContext ctx = new MariaDbContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DbContextBuilder());
```

## Операции над множествами

```csharp
ctx.From<ISimpleEntity>().Select(x => x.Id).IntersectAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
ctx.From<ISimpleEntity>().Select(x => x.Id).ExceptAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
```

```sql
select id from simple_entity
 intersect all
select id from simple_entity
```

## Различия провайдера

MariaDB отличается от [MySQL](mysql.md) только возможностью операций над множествами:

| Аспект | MariaDB |
|---|---|
| Плейсхолдер параметра | `@name` |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| `*ALL` | поддерживается (MariaDB 10.4+) |

## См. также

- [Обзор провайдеров](overview.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbContext.cs`,
`src/nextorm.mariadb/DI/DataContextOptionsBuilderExtensions.cs`,
`test/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `test/nextorm.mariadb.tests/SqlGenerationTests.cs`.
