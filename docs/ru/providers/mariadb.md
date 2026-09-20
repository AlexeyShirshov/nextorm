# Провайдер MariaDB

> Используйте `nextorm.mariadb` для MariaDB 10.4+; он переиспользует отрисовку MySQL и добавляет варианты операций над множествами `INTERSECT ALL`/`EXCEPT ALL`.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [MySQL](mysql.md)

## Обзор

[`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) (`src/nextorm.mariadb/MariaDbDataContext.cs`) наследуется от [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), поэтому
использует тот же драйвер `MySqlConnector` и то же управление соединением и параметрами. Он
возвращает [`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance) из свойства `Dialect`.

[`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) (`src/nextorm.mariadb/MariaDbDialect.cs`) наследуется от [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) и меняет
возможности: `INTERSECT ALL` и `EXCEPT ALL` поддерживаются в MariaDB 10.4 и новее, поэтому
[`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) равно `true`, а MariaDB 10.3+ рендерит оконные
квантили `percentile_cont`/`percentile_disc` (`... within group (order by ...) over (...)`), поэтому
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow) тоже равно `true`. Агрегат произвольного значения
`any_agg` при этом **отключён**, потому что в MariaDB нет `ANY_VALUE` в 10.4–12.x (возможность SQL-2023
`T626` всё ещё ожидается, ориентир — 13.2). Всё остальное (параметры, квотирование, `concat`, coalesce,
разбиение на страницы, имена агрегатов, написание `if(...)` для переносимого `iif`) наследуется без
изменений.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MariaDb;

var builder = new DataContextBuilder()
    .UseMariaDb("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

using var ctx = builder.CreateDataContext();   // IDataContext
```

Также можно создать контекст напрямую:

```csharp
using NextORM.Core;
using NextORM.MariaDb;

using IDataContext ctx = new MariaDbDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
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

MariaDB отличается от [MySQL](mysql.md) возможностью операций над множествами, оконными квантилями и агрегатом произвольного значения:

| Аспект | MariaDB |
|---|---|
| Плейсхолдер параметра | `@name` |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| `*ALL` | поддерживается (MariaDB 10.4+) |
| Текстовый JSON | наследуется от MySQL (`JSON_EXTRACT`/`JSON_SET`) |
| Session/info-функции | наследуются от MySQL (`current_user()`, `session_user()`, `schema()`, `database()`, `version()`) |
| Произвольное значение | не поддерживается (в 10.4–12.x нет `ANY_VALUE`; ожидается MDEV-10426, ориентир — 13.2) |
| Условная функция | наследуется от MySQL (`iif(cond, a, b)` → `if(cond, a, b)`) |
| Оконные квантили | `percentile_cont`/`percentile_disc` как `... within group (order by x) over (...)` (MariaDB 10.3+) |

## См. также

- [Обзор провайдеров](overview.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbDataContext.cs`,
`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`.
