# Хинты запросов

> Добавляйте хинты уровня инструкции к запросу через `Hint(...)`, например SQL Server `OPTION (RECOMPILE)`.

**Предварительные требования:** [Запросы и проекции](../querying/index.md) · [CTE](08-cte.md) · [Обзор провайдеров](../providers/overview.md)

## Обзор

[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) возвращает новую команду с одним или несколькими
хинтами уровня инструкции. Хинты зависят от провайдера: команда хранит обычные строки, а активный
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect) решает, где и как их отрисовать. Метод принимает
`params string[]`, поэтому один или несколько хинтов можно передать одним вызовом или накопить
повторными вызовами; обе формы эквивалентны:

```csharp
// один вызов с несколькими аргументами:
var rows = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString })
    .Hint("recompile", "fast 10")
    .ToList();
```

```csharp
// эквивалентная форма из двух вызовов:
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

`EntityBuilder<T>.WithJoinTableHint(params string[] hints)` прикрепляет такой же табличный хинт к
**последнему добавленному** join — вызывайте его после join и до следующего, как `WithJoinHint`. Это не
то же самое, что [`WithJoinHint`](xref:NextORM.Core.EntityBuilder`1.WithJoinHint(System.String))
(оптимизаторный *join*-хинт вроде `loop`/`hash`/`merge`, рендерится внутри предложения join), и не то
же самое, что `WithTablesInScopeHint` (охватывает каждую физическую таблицу). SQL Server рендерит его
как предложение `WITH (...)` только на этой присоединённой таблице:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Join(dataContext.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
    .WithJoinTableHint("nolock")
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id from complex_entity as [t1] inner join simple_entity with (nolock) as [t2] on t1.id = t2.id
```

В сочетании с `WithTablesInScopeHint` всё равно получается одно предложение `WITH (...)` на этой
таблице: сначала идёт локальный хинт join, затем хинты области видимости (которые по-прежнему
охватывают все остальные физические таблицы):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("holdlock")
    .Join(dataContext.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
    .WithJoinTableHint("nolock")
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id from complex_entity with (holdlock) as [t1] inner join simple_entity with (nolock, holdlock) as [t2] on t1.id = t2.id
```

Хинты рендерятся дословно, поэтому передавайте только доверенные значения. Провайдер включается через
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) и [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); остальные диалекты
отклоняют команду с табличными хинтами через `NotSupportedException`. Вызывайте `WithJoinTableHint`,
чтобы задать табличный хинт конкретному join; `WithTableHint` по-прежнему покрывает только основную
таблицу. Табличный хинт конкретного join можно отрендерить только на join **физической таблицы**:
APPLY-join (а также источник join — производная таблица, табличная функция, XML/pivot или raw-SQL) не имеет
имени таблицы, к которому можно приписать предложение `WITH (...)`, и отклоняется через
`InvalidOperationException`. Если передать только пустые/`null` хинты, ранее прикреплённые хинты остаются
без изменений. И `WithTableHint`, и `WithJoinTableHint` требуют диалект с табличными хинтами, поэтому
SQLite, ClickHouse и провайдер in-memory отклоняют их через `NotSupportedException`.

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

## Хинты join, подзапроса и таблиц в области видимости

`Hint(...)` действует на уровне всей инструкции. Четыре метода построителя прикрепляют хинт к более узкой
части запроса; каждый диалект рендерит доступную ему форму либо отклоняет команду:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .WithTableHint("nolock")
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .WithJoinHint("loop")
    .Select(p => new { p.Item1.Id })
    .ToList();
```

* `WithJoinHint(string hint)` прикрепляет хинт к последнему добавленному join (вызывайте после join и
  до следующего, как `WithStrictness`/`Global`). SQL Server вставляет его внутрь предложения join
  (`inner loop join`, `left hash join`); хинт на `CROSS`/`APPLY`-join отклоняется. PostgreSQL, MySQL и
  MariaDB сворачивают его в комментарий `/*+ ... */` уровня инструкции.
* `WithJoinTableHint(params string[] hints)` прикрепляет блокирующий табличный хинт к последнему
  добавленному join — парный к `WithTableHint` для отдельного join и отличный от оптимизаторного
  `WithJoinHint` выше. SQL Server рендерит предложение `WITH (hint, ...)` на этой присоединённой
  таблице; остальные диалекты отклоняют его через `NotSupportedException`. Его можно применить только к
  join физической таблицы: APPLY-join, производная таблица, табличная функция или XML/pivot-источник join
  отклоняются через `InvalidOperationException`. См. раздел
  «Блокирующие табличные хинты».
* `WithSubQueryHint(string hint)` прикрепляет хинт к источнику-производной таблице построителя
  `From(subQuery)`. PostgreSQL/MySQL/MariaDB сворачивают его в `/*+ ... */`; SQL Server отклоняет —
  T-SQL не позволяет добавить query hint к подзапросу.
* `WithTablesInScopeHint(params string[] hints)` применяет хинты к каждой физической таблице области
  видимости запроса. SQL Server добавляет предложение `WITH (hint, ...)` к основной и каждой
  присоединённой таблице (многотабличный аналог `WithTableHint`); PostgreSQL/MySQL/MariaDB сворачивают
  его в комментарий уровня инструкции.

```sql
-- SQL Server:
select t1.id from simple_entity as [t1] inner loop join complex_entity as [t2] on t1.id = t2.id
-- PostgreSQL (pg_hint_plan):
select /*+ HashJoin(t1 t2) */ id from simple_entity as "t1" join complex_entity as "t2" ...
-- MySQL 8:
select /*+ JOIN_ORDER(t1, t2) */ id from simple_entity as `t1` join complex_entity as `t2` ...
```

Пустой хинт отклоняется через `ArgumentException`, а join-хинт без предшествующего join бросает
`InvalidOperationException`. Для диалектов с inline-комментарием текст хинта рендерится как есть, поэтому
пишите псевдонимы источников сами (`HashJoin(t1 t2)`): псевдонимы nextorm назначаются на этапе рендера и
наружу не отдаются. Все четыре хинта входят в ключ плана. В SQLite, ClickHouse и провайдере in-memory они
отклоняются через `NotSupportedException`.

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

* Блокирующие табличные хинты для таблицы из `FROM` запроса рендерит [`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])); для выбранного join используйте [`WithJoinTableHint`](xref:NextORM.Core.EntityBuilder`1.WithJoinTableHint(System.String[])), а чтобы покрыть все физические таблицы — `WithTablesInScopeHint`.
* Объединение команды с хинтами через операцию над множествами ([`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})), ...) не
  защищено; хинт «уезжает» в ту ветку, к которой был привязан, и этого следует избегать.

## См. также

- [Соединения](02-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})).
- [CTE](08-cte.md) - `maxRecursion` и предложение SQL Server `option (maxrecursion n)`.
- [Запросы и проекции](../querying/index.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/Builders/EntityBuilder.cs` (`WithTableHint`/`WithJoinTableHint`/`WithJoinHint`/`WithSubQueryHint`/`WithTablesInScopeHint`),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB наследует).
