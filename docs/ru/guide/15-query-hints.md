# Хинты запросов

> Передайте оптимизатору хинт для всей инструкции через `Hint(...)`. Если нужно находить инструкцию в логах или профилировщике, используйте `WithTag(...)`.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [CTE](10-cte.md) · [Обзор провайдеров](../connecting-providers/overview.md)

## Выберите тип хинта

- Чтобы передать хинт оптимизатору для всей инструкции, используйте [`Hint(...)`](#обзор).
- Чтобы пометить инструкцию для поиска в логах или профилировщике, используйте [`WithTag(...)`](#метки-запроса).
- Чтобы задать блокировку или использование индекса для таблицы, см. разделы [Блокирующие табличные хинты](#блокирующие-табличные-хинты) и [Index-хинты](#index-хинты).
- Чтобы настроить отдельный join, подзапрос или все таблицы в области видимости, см. [Хинты join, подзапроса и таблиц в области видимости](#хинты-join-подзапроса-и-таблиц-в-области-видимости).
- Модификаторы запросов ClickHouse — отдельные возможности, не хинты. См. [Модификаторы запроса ClickHouse](#модификаторы-запроса-clickhouse).

## Обзор

Используйте хинт уровня инструкции, когда нужно повлиять на то, как база данных строит план запроса.
[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) возвращает новую команду с одним или
несколькими хинтами. Активный [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) определяет, где и как они
попадут в SQL, поэтому синтаксис зависит от провайдера.

Поскольку `Hint` принимает `params string[]`, передайте несколько хинтов одним вызовом или добавляйте их
повторными вызовами. Результат одинаков:

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
select id, somestring
 from complex_entity where (id > 1) option (recompile, fast 10)
```

Пустые хинты игнорируются. Список хинтов входит в ключ плана запроса. Команда с хинтами не использует
кэшированный план такой же команды без хинтов; команды с разными хинтами также используют разные планы.

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

Чтобы прикрепить блокирующие хинты SQL Server (`nolock`/`updlock`/`holdlock`) к основной физической
таблице, используйте `FromOptions.WithTableHint(params string[] hints)`. SQL Server помещает их в
предложение `WITH (...)` между именем таблицы и её псевдонимом:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithTableHint("nolock"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id
 from complex_entity with (nolock)
```

Чтобы прикрепить блокирующий хинт к одной присоединённой таблице, передайте его через завершающую
лямбду join с помощью
[`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])).
Этот метод отличается от [`JoinOptions.WithJoinHint`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)),
который задаёт оптимизаторный *join*-хинт, например `loop`, `hash` или `merge`, внутри предложения join.
Он также отличается от `WithTablesInScopeHint`, который охватывает все физические таблицы. SQL Server
помещает табличный хинт в предложение `WITH (...)` только для этой присоединённой таблицы:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Join<ISimpleEntity>((a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id
 from complex_entity as [t1]
 inner join simple_entity with (nolock) as [t2] on t1.id = t2.id
```

При сочетании с `WithTablesInScopeHint` SQL Server по-прежнему выдаёт одно предложение `WITH (...)` для
этой таблицы. Сначала идёт локальный хинт join, затем хинты области видимости. Последние также
охватывают остальные физические таблицы:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("holdlock")
    .Join<ISimpleEntity>((a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id
 from complex_entity with (holdlock) as [t1]
 inner join simple_entity with (nolock, holdlock) as [t2] on t1.id = t2.id
```

Хинты попадают в SQL без изменений, поэтому передавайте только доверенные значения. Провайдер включает поддержку через
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) и [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); остальные диалекты
отклоняют команду с табличными хинтами через `NotSupportedException`. Передавайте `j => j.WithJoinTableHint(...)`,
чтобы задать табличный хинт конкретному join; `WithTableHint` по-прежнему покрывает только основную
таблицу. Табличный хинт конкретного join можно отрендерить только на join **физической таблицы**:
APPLY-join (а также источник join — производная таблица, табличная функция, XML/pivot или raw-SQL) не имеет
имени таблицы, к которому можно приписать предложение `WITH (...)`, и отклоняется через
`InvalidOperationException`. Если передать только пустые/`null` хинты, ранее прикреплённые хинты остаются
без изменений. И `WithTableHint`, и `WithJoinTableHint` требуют диалект с табличными хинтами, поэтому
SQLite, ClickHouse и провайдер in-memory отклоняют их через `NotSupportedException`.

В PostgreSQL нет синтаксиса табличных хинтов `WITH (...)` — это конструкция T-SQL. Чтобы заблокировать
одну таблицу в PostgreSQL или MySQL, используйте блокировку источника
([`FromOptions.ForUpdate`](xref:NextORM.Core.FromOptions.ForUpdate)/[`FromOptions.ForShare`](xref:NextORM.Core.FromOptions.ForShare)
или [`JoinOptions.ForUpdate`](xref:NextORM.Core.JoinOptions.ForUpdate)/[`JoinOptions.ForShare`](xref:NextORM.Core.JoinOptions.ForShare)
  для join): она формирует в SQL `FOR UPDATE OF <alias>`/`FOR SHARE OF <alias>` — см.
[Блокировка строк](31-provider-specific-features.md#блокировка-строк-for-update--for-share).
Оптимизаторные хинты через `pg_hint_plan` — отдельная поверхность `Hint(...)`, описанная выше.

## Index-хинты

Чтобы подсказать планировщику, какой индекс рассмотреть для основной физической таблицы, используйте
`FromOptions.WithIndex(params string[] indexes)`. Перегрузка
`WithIndex(IndexHintKind kind, params string[] indexes)` задаёт намерение
([`IndexHintKind`](xref:NextORM.Core.IndexHintKind).`Use`/`Force`/`Ignore`), а `WithoutIndex()` запрещает
использовать индекс. Каждый диалект формирует свой вариант SQL после имени таблицы и перед её псевдонимом:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithIndex("ix_complex_id"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
-- MySQL/MariaDB  (также `force index (...)` / `ignore index (...)`):
select id
 from complex_entity use index (ix_complex_id)
-- SQLite  (ровно один индекс; `WithoutIndex()` рендерит `not indexed`):
select id
 from complex_entity indexed by ix_complex_id
-- SQL Server  (сливается в единственное предложение табличного хинта):
select id
 from complex_entity with (index(ix_complex_id))
```

Поддержка зависит от диалекта. Провайдер включает её через [`ISqlDialect.IndexHints`](xref:NextORM.Core.ISqlDialect.IndexHints) и
[`IIndexHintRenderer`](xref:NextORM.Core.IIndexHintRenderer) (MySQL/MariaDB, SQLite, SQL Server).
PostgreSQL (без `pg_hint_plan`), ClickHouse и провайдер in-memory не имеют нативного index-хинта и
отклоняют команду с ним через `NotSupportedException`. В SQL Server index-хинт сливается с блокирующим
`WithTableHint` в одно предложение `with (nolock, index(...))`, а `Ignore` отклоняется (хинта
«игнорировать индекс» там нет); SQLite принимает ровно одно имя индекса (его `INDEXED BY` берёт один).
Имена рендерятся дословно, поэтому передавайте только доверенные значения, а список хинтов входит в
ключ плана.

## Метки запроса

`WithTag(string? tag)` прикрепляет к запросу произвольную метку. В отличие от `Hint`, метка — это
обычный SQL-комментарий (`/* tag */`), а не хинт оптимизатора. Она доступна на **всех**
SQL-провайдерах и появляется в SQL сразу после ключевого слова `SELECT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTag("reports.orders")
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select /* reports.orders */ id
 from complex_entity where (id > 1)
```

Используйте метку, чтобы найти инструкцию в профилировщике, серверном логе или хранилище запросов,
например `pg_stat_activity`, SQL Server Query Store или ClickHouse `system.query_log`.

Метка входит в ключ плана, поэтому команды с разными метками не используют один кэшированный план.
Передача `null` или пустой строки очищает метку. Провайдер in-memory принимает вызов и игнорирует его,
поскольку не генерирует SQL.

nextorm нейтрализует переносы строк и разделители комментариев `*/` и `/*`. SQL Server допускает
вложенные блочные комментарии, поэтому nextorm обрабатывает и `/*`. Комментарий всегда начинается с
пробела: метка, начинающаяся с `!` или `+`, не становится исполняемым комментарием или подсказкой
оптимизатора MySQL/MariaDB. Если у запроса есть и `Hint(...)`, комментарий-хинт ставится первым
(`select /*+ hint */ /* tag */ ...`), чтобы его распознали `pg_hint_plan` и оптимизатор MySQL/MariaDB.

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
// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id
// from simple_entity
var pg = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("SeqScan(simple_entity)");

// MySQL / MariaDB:  select /*+ MAX_EXECUTION_TIME(1000) */ id
// from simple_entity
var my = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("MAX_EXECUTION_TIME(1000)");
```

Провайдер включается через [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) и [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase));
построитель отклоняет команду с хинтами у диалекта, который сообщает `false`.

## Хинты join, подзапроса и таблиц в области видимости

`Hint(...)` действует на всю инструкцию. Если нужно задать хинт для отдельного join, подзапроса или
области таблиц, используйте методы ниже. Каждый диалект формирует только поддерживаемые им варианты SQL
и отклоняет остальные:

```csharp
var rows = dataContext.From<ISimpleEntity>(o => o.WithTableHint("nolock"))
    .Join<IComplexEntity>((s, c) => s.Id == c.Id, j => j.WithJoinHint("loop"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

* [`JoinOptions.WithJoinHint(string hint)`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)) прикрепляет хинт к join (`j => j.WithJoinHint("loop")`) при его объявлении. SQL Server вставляет его в предложение join (`inner loop join`, `left hash join`). Хинт для `CROSS`/`APPLY`-join отклоняется. PostgreSQL, MySQL и MariaDB включают его в комментарий `/*+ ... */` уровня инструкции.
* [`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) прикрепляет блокирующий табличный хинт к join,
  в котором объявлен, — парный к `WithTableHint` для отдельного join и отличный от оптимизаторного
  `WithJoinHint` выше. SQL Server рендерит предложение `WITH (hint, ...)` на этой присоединённой
  таблице; остальные диалекты отклоняют его через `NotSupportedException`. Его можно применить только к
  join физической таблицы: APPLY-join, производная таблица, табличная функция или XML/pivot-источник join
  отклоняются через `InvalidOperationException`. См. раздел
  «Блокирующие табличные хинты».
* [`FromOptions.WithSubQueryHint(string hint)`](xref:NextORM.Core.FromOptions.WithSubQueryHint(System.String)) прикрепляет хинт к источнику-производной таблице построителя `From(subQuery, o => o.WithSubQueryHint(...))`. PostgreSQL/MySQL/MariaDB включают его в `/*+ ... */`. SQL Server отклоняет его, потому что T-SQL не позволяет добавить query hint к подзапросу.
* `WithTablesInScopeHint(params string[] hints)` применяет хинты к каждой физической таблице области
  видимости запроса. SQL Server добавляет предложение `WITH (hint, ...)` к основной и каждой
  присоединённой таблице (многотабличный аналог `WithTableHint`); PostgreSQL/MySQL/MariaDB сворачивают
  его в комментарий уровня инструкции.

```sql
-- SQL Server:
select t1.id
 from simple_entity as [t1] inner loop
 join complex_entity as [t2] on t1.id = t2.id
-- PostgreSQL (pg_hint_plan):
select /*+ HashJoin(t1 t2) */ id
 from simple_entity as "t1"
 join complex_entity as "t2" ...
-- MySQL 8:
select /*+ JOIN_ORDER(t1, t2) */ id
 from simple_entity as `t1`
 join complex_entity as `t2` ...
```

Пустой хинт отклоняется через `ArgumentException` при выполнении лямбды. Диалекты со встроенными
комментариями вставляют текст хинта без изменений. Поэтому укажите псевдонимы источников сами
(`HashJoin(t1 t2)`): nextorm назначает их при формировании SQL и не предоставляет вызывающему коду.
Все четыре хинта входят в ключ плана. SQLite, ClickHouse и провайдер in-memory отклоняют их через
`NotSupportedException`.

### Табличные хинты области видимости в JOIN-формах DELETE/UPDATE

`WithTablesInScopeHint` учитывается построителями многотабличных `DELETE` и `UPDATE`, а не только
`SELECT`: хинты попадают на целевую и каждую присоединённую физическую таблицу.

```csharp
dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("SeqScan(t1)")        // SQL Server: .WithTablesInScopeHint("nolock")
    .Join<ISimpleEntity>((c, s) => c.Id == s.Id)
    .CreateUpdateJoinBuilder()
    .Set(p => p.Item1.String, "x")
    .ToSql();
```

```sql
-- SQL Server: предложение WITH (...) на каждой физической таблице
update [t1] set t1.somestring = @p0 from complex_entity with (nolock) as [t1] join simple_entity with (nolock) as [t2] on t1.id = t2.id
-- PostgreSQL: один комментарий уровня инструкции сразу после UPDATE (так же для DELETE)
update /*+ SeqScan(t1) */ complex_entity as "t1" set somestring = @p0 from simple_entity as "t2" where t1.id = t2.id
```

В PostgreSQL/MySQL/MariaDB хинт области видимости — вместе с собранными join/subquery-хинтами — выводится
один раз комментарием `/*+ ... */` сразу после ключевого слова `DELETE`/`UPDATE`. Для DML-инструкции с
CTE поднятый префикс `WITH` ставится перед инструкцией, а комментарий остаётся после DML-глагола — так
же, как в `SELECT` он ставится после `SELECT` в `WITH … SELECT`. SQLite и ClickHouse отклоняют непустой
scope-хинт через `NotSupportedException`, как и в `SELECT`; сама JOIN-форма `DELETE`/`UPDATE` в этих
провайдерах не поддерживается, поэтому их текущая диагностика о возможности не меняется. DML без хинтов
остаётся байт-в-байт прежним.

## Модификаторы запроса ClickHouse

В ClickHouse есть четыре модификатора уровня запроса — это не хинты. Используйте `Final()`,
`PreWhere(predicate)` и `Settings(("key", "value"), ...)` как отдельные методы билдера. `Sample(ratio[, offset])`
задаётся в `From` как опция источника для конкретного запроса.

Эти возможности доступны только в ClickHouse как методы-расширения пакета `nextorm.clickhouse`.
Добавьте `using NextORM.ClickHouse;` в файл, где вы их используете. Остальные провайдеры и контекст
in-memory выбрасывают `NotSupportedException`.

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
select id, nullableint
 from complex_entity final sample 0.1 offset 0.5
prewhere (nullableint > 0)
where (b = true)
settings max_threads = 2
```

`Settings` замыкает порядок предложений: он выводится после `LIMIT ... BY` и после
`LIMIT`/`OFFSET`, а `LIMIT` после `SETTINGS` ClickHouse отклоняет (код сервера 62). Скалярный
терминал добавляет постраничность — `First`/`FirstOrDefault` рендерят `limit 1`,
`Single`/`SingleOrDefault` рендерят `limit 2` — поэтому предложение настроек идёт за ней:

```csharp
var count = dataContext.From<IComplexEntity>()
    .Settings(("max_threads", "2"))
    .Select(c => SqlFunctions.Sql.count())
    .First();
```

```sql
select toInt32(count(*))
 from complex_entity
limit 1
 settings max_threads = 2
```

`Final()` принудительно выполняет merge ReplacingMergeTree/CollapsingMergeTree перед чтением; `Sample`
читает долю строк `[0, 1]`; `PreWhere` фильтрует до обычного `WHERE`, позволяя пропустить чтение
остальных колонок; `Settings` добавляет завершающее предложение, значения которого рендерятся как есть
(передавайте только доверенные литералы). `FINAL` и `PREWHERE` требуют движка таблицы, который их
поддерживает — движок `Memory` отклоняет оба.

## Ограничения

* Блокирующие табличные хинты для таблицы из `FROM` запроса рендерит [`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])); для выбранного join используйте [`JoinOptions.WithJoinTableHint`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])), а чтобы покрыть все физические таблицы — `WithTablesInScopeHint`.
* Операции над множествами не защищают от объединения команды с хинтом. Хинт остаётся в ветке, к которой
  он привязан, поэтому не объединяйте такие команды через [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})) или подобные операции.

## См. также

- [Соединения](04-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).
- [CTE](10-cte.md) - `maxRecursion` и предложение SQL Server `option (maxrecursion n)`.
- [Запросы и проекции](../engine/index.md)

---

Source: `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/DataContext/Sql/FromOptions.cs` (`WithTableHint`/`WithIndex`/`WithoutIndex`),
`src/nextorm.core/Builders/JoinOptions.cs` (`WithJoinTableHint`/`WithJoinHint`), `src/nextorm.core/DataContext/Sql/FromOptions.cs` (`WithSubQueryHint`), `src/nextorm.core/Builders/Entity/EntityBuilder.cs` (`WithTablesInScopeHint`),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB наследует).
