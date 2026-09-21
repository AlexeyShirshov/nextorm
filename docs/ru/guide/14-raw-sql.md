# Необработанный SQL (Raw SQL)

> Заменяйте генерируемый SQL запроса вручную написанным текстом, сохраняя сопоставление строк nextorm.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Переиспользование запросов: кэш против Prepare](15-query-reuse.md)

## Обзор

[`WithSql`](xref:NextORM.Core.EntityBuilder`1) и [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) позволяют сохранить обычный типизированный запрос как **форму результата** и
подставить необработанную инструкцию для выполнения. Всё остальное — проекция, сопоставление сущности,
конструирование через инициализацию членов, вложенные DTO — берётся из запроса, построенного до подстановки.

```csharp
// QueryCommand<TResult>
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params);

public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);

// EntityBuilder<TResult> convenience overloads
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql, object? @params);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this EntityBuilder<TResult> entity, string sql);
```

* [`WithSql`](xref:NextORM.Core.EntityBuilder`1) возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), который вы выполняете обычными терминалами
  ([`ToListAsync`](xref:NextORM.Core.EntityBuilder`1), [`FirstAsync`](xref:NextORM.Core.EntityBuilder`1), ...). Он проходит через неявный кэш планов, как и любая другая команда.
* [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1); выполняйте его через перегрузки контекста
  (`dataContext.ToListAsync(prepared, ...)`, `dataContext.FirstAsync(prepared, ...)`, ...).
* `@params` — это обычный объект. Его **открытые свойства экземпляра** становятся именованными параметрами
  в порядке свойств, причём имя свойства используется как имя параметра.
* `mode` — это `[Flags]`-значение: [`None`](xref:NextORM.Core.PrepareFromSqlMode.None) (по умолчанию) — буферизованное/скалярное
  выполнение (как `nonStreamUsing: true` в `Prepare(...)`), [`Streaming`](xref:NextORM.Core.PrepareFromSqlMode.Streaming) требуется для потокового
  (небуферизованного) чтения, а [`StoreInCache`](xref:NextORM.Core.PrepareFromSqlMode.StoreInCache) заполняет кэш планов. Все перегрузки по умолчанию
  используют `None`, поэтому необработанный SQL не заполняет кэш планов без явного запроса.

Необработанная инструкция передаётся дословно, включая комментарии. Заполнители параметров должны
соответствовать тому, что ожидает базовый провайдер ADO.NET (`@name` для SQL Server/PostgreSQL;
Microsoft.Data.Sqlite также принимает `@name`, хотя генерируемый nextorm SQL для SQLite использует
`$name`).

## [`WithSql`](xref:NextORM.Core.EntityBuilder`1)

```csharp
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity --this is custom sql")
    .ToListAsync();
```

```sql
-- executed as written
select id from simple_entity --this is custom sql
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Необработанная инструкция с именованными параметрами:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity where id = @id", new { id = 1 })
    .ToListAsync();
```

```sql
select id from simple_entity where id = @id
-- @id is bound from the property `id` of the params object
```

Вывод:

| Id |
|----|
| 1 |

## [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1)

Подготовьте необработанную инструкцию и выполните её в контексте. Параметры времени выполнения передаются
во время выполнения точно так же, как для `Prepare(...)`:

```csharp
var prepared = dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .PrepareFromSql("select id from simple_entity", cancellationToken);

var ids = await dataContext.ToListAsync(prepared);
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Параметр из объекта params плюс параметр времени выполнения (`@norm_p0`), переданный в терминал:

```csharp
var prepared = dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, cancellationToken);

var entity = await dataContext.FirstAsync(prepared, 1);
// id = 1 + 1 = 2
```

Вывод:

| Id |
|----|
| 2 |

## Сопоставление результата

Тип результата определяется запросом, который вы строите **до** подстановки SQL:

```csharp
// scalar
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity")
    .ToListAsync();

// entity member-init
var entities = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

// DTO
var dtos = await dataContext.From<SimpleEntity>()
    .Select(it => new IdDto { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

public sealed class IdDto
{
    public int Id { get; set; }
}
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Имена столбцов в необработанном списке `select` сопоставляются с этой проекцией, поэтому они должны точно
совпадать с сопоставленными именами столбцов (или именами `[Column]`).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Инструкция передаётся дословно; параметры связываются по имени (`@name` работает с `Microsoft.Data.Sqlite`; генерируемый SQL обычно использует `$name`). |
| SQL Server | Инструкция передаётся дословно; параметры `@name`. |
| PostgreSQL | Инструкция передаётся дословно; параметры `@name`. |
| MySQL | Инструкция передаётся дословно; параметры `@name`. |
| MariaDB | Инструкция передаётся дословно; параметры `@name`. |
| ClickHouse | Инструкция передаётся дословно; параметры `@name` (драйвер переписывает их в `{name:Type}`). |
| In-memory | [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1) не поддерживается ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) бросает `NotSupportedException`); для необработанных инструкций используйте SQL-провайдер. |

## См. также

* [Переиспользование запросов: кэш против Prepare](15-query-reuse.md) — компромиссы `nonStreamUsing` / `storeInCache`.
* [Скалярные функции](11-scalar-functions.md) — оставайтесь в LINQ вместо перехода к необработанному SQL.
* [Обзор провайдеров](../providers/overview.md) — заполнитель параметра для каждого провайдера.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/RawSqlOverride.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`;
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`.
