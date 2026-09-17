# Необработанный SQL (Raw SQL)

> Заменяйте генерируемый SQL запроса вручную написанным текстом, сохраняя сопоставление строк nextorm.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Переиспользование запросов: кэш против Prepare](15-query-reuse.md)

## Обзор

`WithSql` и `PrepareFromSql` позволяют сохранить обычный типизированный запрос как **форму результата** и
подставить необработанную инструкцию для выполнения. Всё остальное — проекция, сопоставление сущности,
конструирование через инициализацию членов, вложенные DTO — берётся из запроса, построенного до подстановки.

```csharp
// QueryCommand<TResult>
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params);

public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, bool nonStreamUsing, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken = default);

// Entity<TResult> convenience overloads
public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql, object? @params);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this Entity<TResult> entity, string sql);
```

* `WithSql` возвращает `QueryCommand<TResult>`, который вы выполняете обычными терминалами
  (`ToListAsync`, `FirstAsync`, ...). Он проходит через неявный кэш планов, как и любая другая команда.
* `PrepareFromSql` возвращает `IPreparedQueryCommand<TResult>`; выполняйте его через перегрузки контекста
  (`dataContext.ToListAsync(prepared, ...)`, `dataContext.FirstAsync(prepared, ...)`, ...).
* `@params` — это обычный объект. Его **открытые свойства экземпляра** становятся именованными параметрами
  в порядке свойств, причём имя свойства используется как имя параметра.
* `nonStreamUsing` имеет то же значение, что и в `Prepare(...)`: `true` (по умолчанию) — для
  буферизованных/скалярных терминалов, `false` требуется для потоковой передачи. `storeInCache` равно
  **false** для всех перегрузок `PrepareFromSql`, кроме пятиаргументной, поэтому необработанный SQL,
  подготовленный таким образом, по умолчанию не заполняет кэш планов.

Необработанная инструкция передаётся дословно, включая комментарии. Заполнители параметров должны
соответствовать тому, что ожидает базовый провайдер ADO.NET (`@name` для SQL Server/PostgreSQL;
Microsoft.Data.Sqlite также принимает `@name`, хотя генерируемый nextorm SQL для SQLite использует
`$name`).

## `WithSql`

```csharp
var ids = await dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity --this is custom sql")
    .ToListAsync();
```

```sql
-- executed as written
select id from simple_entity --this is custom sql
```

Необработанная инструкция с именованными параметрами:

```csharp
var rows = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity where id = @id", new { id = 1 })
    .ToListAsync();
```

```sql
select id from simple_entity where id = @id
-- @id is bound from the property `id` of the params object
```

## `PrepareFromSql`

Подготовьте необработанную инструкцию и выполните её в контексте. Параметры времени выполнения передаются
во время выполнения точно так же, как для `Prepare(...)`:

```csharp
var prepared = dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .PrepareFromSql("select id from simple_entity", cancellationToken);

var ids = await dataContext.ToListAsync(prepared);
```

Параметр из объекта params плюс параметр времени выполнения (`@norm_p0`), переданный в терминал:

```csharp
var prepared = dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, cancellationToken);

var entity = await dataContext.FirstAsync(prepared, 1);
// id = 1 + 1 = 2
```

## Сопоставление результата

Тип результата определяется запросом, который вы строите **до** подстановки SQL:

```csharp
// scalar
var ids = await dataContext.Create<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity")
    .ToListAsync();

// entity member-init
var entities = await dataContext.Create<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

// DTO
var dtos = await dataContext.Create<SimpleEntity>()
    .Select(it => new IdDto { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

public sealed class IdDto
{
    public int Id { get; set; }
}
```

Имена столбцов в необработанном списке `select` сопоставляются с этой проекцией, поэтому они должны точно
совпадать с сопоставленными именами столбцов (или именами `[Column]`).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Инструкция передаётся дословно; параметры связываются по имени (`@name` работает с `Microsoft.Data.Sqlite`; генерируемый SQL обычно использует `$name`). |
| SQL Server | Инструкция передаётся дословно; параметры `@name`. |
| PostgreSQL | Инструкция передаётся дословно; параметры `@name`. |
| In-memory | `PrepareFromSql` не реализован (`InMemoryContext` бросает `NotImplementedException`); для необработанных инструкций используйте SQL-провайдер. |

## См. также

* [Переиспользование запросов: кэш против Prepare](15-query-reuse.md) — компромиссы `nonStreamUsing` / `storeInCache`.
* [Скалярные функции](11-scalar-functions.md) — оставайтесь в LINQ вместо перехода к необработанному SQL.
* [Обзор провайдеров](../providers/overview.md) — заполнитель параметра для каждого провайдера.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/DbQueryCommandExtension.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`;
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`.
