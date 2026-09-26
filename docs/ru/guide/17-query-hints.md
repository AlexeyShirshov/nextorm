# Хинты запросов

> Добавляйте хинты уровня инструкции к запросу через `Hint(...)`, например SQL Server `OPTION (RECOMPILE)`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [CTE](09-cte.md) · [Обзор провайдеров](../providers/overview.md)

## Обзор

[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) возвращает новую команду с одним или несколькими
хинтами уровня инструкции. Хинты зависят от провайдера: команда хранит обычные строки, а активный
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect) решает, где и как их отрисовать. Повторный вызов накапливает хинты:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString })
    .Hint("recompile")
    .Hint("fast 10")
    .ToList();
```

```sql
select id, somestring from complex_entity where (id > 1) option (recompile, fast 10)
```

Пустые хинты игнорируются. Список хинтов входит в ключ плана запроса, поэтому команда с хинтами
никогда не переиспользует кэшированный план идентичной команды без них (и наоборот), а две команды с
разными хинтами не делят один план.

## Сочетание с рекурсивным CTE

SQL Server допускает только одно предложение `OPTION` на инструкцию. Когда запрос также объявил
ограничение рекурсии CTE, хинты сливаются в это же предложение:

```csharp
var sql = dataContext.WithRecursive("nums", body, 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .Hint("recompile");
```

```sql
-- заканчивается на:
... option (maxrecursion 100, recompile)
```

## Блокирующие табличные хинты

`EntityBuilder<T>.WithTableHint(params string[] hints)` прикрепляет блокирующие хинты SQL Server (`nolock`/`updlock`/`holdlock`) к основной
физической таблице. SQL Server рендерит их как предложение `WITH (...)`, между именем таблицы и её
псевдонимом:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTableHint("nolock")
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity with (nolock)
```

Хинты рендерятся дословно, поэтому передавайте только доверенные значения. Провайдер включается через
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) и [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); остальные диалекты
отклоняют команду с табличными хинтами через `NotSupportedException`. Покрыта только основная
таблица; хинты на присоединённых таблицах пока не входят в API.

## Index-хинты

`EntityBuilder<T>.WithIndex(params string[] indexes)` просит планировщик рассмотреть именованный индекс
основной физической таблицы. Перегрузка `WithIndex(IndexHintKind kind, params string[] indexes)` задаёт
намерение ([`IndexHintKind`](xref:NextORM.Core.IndexHintKind).`Use`/`Force`/`Ignore`), а `WithoutIndex()`
подавляет использование индексов. Каждый диалект рендерит свою нативную форму после имени таблицы и до
её псевдонима:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithIndex("ix_complex_id")
    .Select(c => new { c.Id })
    .ToList();
```

```sql
-- MySQL/MariaDB  (также `force index (...)` / `ignore index (...)`):
select id from complex_entity use index (ix_complex_id)
-- SQLite  (ровно один индекс; `WithoutIndex()` рендерит `not indexed`):
select id from complex_entity indexed by ix_complex_id
-- SQL Server  (сливается в единственное предложение табличного хинта):
select id from complex_entity with (index(ix_complex_id))
```

Провайдер включается через [`ISqlDialect.IndexHints`](xref:NextORM.Core.ISqlDialect.IndexHints) и
[`IIndexHintRenderer`](xref:NextORM.Core.IIndexHintRenderer) (MySQL/MariaDB, SQLite, SQL Server).
PostgreSQL (без `pg_hint_plan`), ClickHouse и провайдер in-memory не имеют нативного index-хинта и
отклоняют команду с ним через `NotSupportedException`. В SQL Server index-хинт сливается с блокирующим
`WithTableHint` в одно предложение `with (nolock, index(...))`, а `Ignore` отклоняется (хинта
«игнорировать индекс» там нет); SQLite принимает ровно одно имя индекса (его `INDEXED BY` берёт один).
Имена рендерятся дословно, поэтому передавайте только доверенные значения, а список хинтов входит в
ключ плана.

## Метки запроса

`WithTag(string? tag)` прикрепляет к запросу произвольную метку. В отличие от `Hint`, метка — это
обычный SQL-комментарий (`/* tag */`), а не оптимизаторный хинт, поэтому она рендерится на **всех**
SQL-провайдерах и не гейтится. Комментарий вставляется сразу после ключевого слова `SELECT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTag("reports.orders")
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select /* reports.orders */ id from complex_entity where (id > 1)
```

Метка позволяет опознать запрос в профилировщике, серверном логе или серверном хранилище запросов
(`pg_stat_activity`, SQL Server Query Store, ClickHouse `system.query_log`). Переносы строк и
разделители комментария `*/` и `/*` нейтрализуются — SQL Server **вкладывает** блочные комментарии,
поэтому экранируется и `/*`; комментарий всегда открывается пробелом, так что метка, начинающаяся с
`!` или `+`, не превратится в executable-комментарий или optimizer-hint MySQL/MariaDB. Метка входит в
ключ плана, поэтому две команды, различающиеся только меткой, не делят кэшированный план; `null` или
пустая строка очищают метку. Провайдер in-memory принимает вызов и игнорирует его (SQL не генерируется).
Если у запроса есть ещё и `Hint(...)`, комментарий-хинт рендерится первым
(`select /*+ hint */ /* tag */ ...`), чтобы `pg_hint_plan` и оптимизатор MySQL/MariaDB его распознали.

## Провайдеры

| Провайдер | Хинты запросов |
|---|---|
| SQL Server | Поддерживаются: рендерятся как завершающее предложение `OPTION (hint, ...)`. |
| PostgreSQL | Поддерживаются: рендерятся как встроенный комментарий `/*+ hint ... */` сразу после `SELECT` — в позиции, которую читает опциональное расширение `pg_hint_plan`; без расширения это обычный комментарий. |
| MySQL / MariaDB | Поддерживаются: рендерятся как встроенный комментарий-optimizer-hint `/*+ hint ... */` сразу после `SELECT`. |
| SQLite | Не поддерживаются: построение SQL выбрасывает `NotSupportedException`. |
| ClickHouse | Не поддерживаются: используйте `Settings(...)`; `Hint(...)` выбрасывает `NotSupportedException`. |

Несколько хинтов объединяются в один комментарий через пробел — форма, которую ожидают и
`pg_hint_plan`, и оптимизатор MySQL/MariaDB:

```csharp
// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id from simple_entity
var pg = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("SeqScan(simple_entity)");

// MySQL / MariaDB:  select /*+ MAX_EXECUTION_TIME(1000) */ id from simple_entity
var my = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("MAX_EXECUTION_TIME(1000)");
```

Провайдер включается через [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) и [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase));
построитель отклоняет команду с хинтами у диалекта, который сообщает `false`.

## Модификаторы запроса ClickHouse

ClickHouse имеет четыре модификатора уровня запроса — не хинты: `Final()`, `PreWhere(predicate)` и
`Settings(("key", "value"), ...)` — отдельные методы построителя, а модификатор `Sample(ratio[, offset])`
— это опция источника на время запроса, задаваемая в `From`. Они допустимы только в ClickHouse;
остальные провайдеры и контекст in-memory бросают `NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.Sample(0.1, 0.5))
    .Final()
    .PreWhere(c => c.Int > 0)
    .Where(c => c.Boolean == true)
    .Select(c => new { c.Id, c.Int })
    .Settings(("max_threads", "2"))
    .ToList();
```

```sql
select id, nullableint from complex_entity final sample 0.1 offset 0.5
prewhere (nullableint > 0)
where (b = true)
settings max_threads = 2
```

`Final()` принудительно выполняет merge ReplacingMergeTree/CollapsingMergeTree перед чтением; `Sample`
читает долю строк `[0, 1]`; `PreWhere` фильтрует до обычного `WHERE`, позволяя пропустить чтение
остальных колонок; `Settings` добавляет завершающее предложение, значения которого рендерятся как есть
(передавайте только доверенные литералы). `FINAL` и `PREWHERE` требуют движка таблицы, который их
поддерживает — движок `Memory` отклоняет оба.

## Ограничения

* Блокирующие табличные хинты рендерятся только для основной таблицы; хинты на присоединённой таблице пока не
  входят в API ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])) применяется к таблице из `FROM` запроса).
* Объединение команды с хинтами через операцию над множествами ([`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})), ...) не
  защищено; хинт «уезжает» в ту ветку, к которой был привязан, и этого следует избегать.

## См. также

- [Соединения](03-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})).
- [CTE](09-cte.md) - `maxRecursion` и предложение SQL Server `option (maxrecursion n)`.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB наследует).
