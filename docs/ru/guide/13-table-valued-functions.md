# Табличные функции

> Объявляйте табличную функцию базы данных как runtime-дескриптор и запрашивайте её как источник
> `FROM`, соединения или APPLY через `ctx.From(() => Db.MyTvf(...))`, сопоставляя её строки как любую
> другую сущность.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Соединения](04-joins.md) · [Группировка и агрегаты](05-grouping-and-aggregates.md)

## Обзор

Источник табличной функции (TVF) — обычный статический фабричный метод, возвращающий дескриптор
[`TvfEntityBuilder<T>`](xref:NextORM.Core.TvfEntityBuilder`1):

```csharp
public static TvfEntityBuilder<ITvfRow> AllRows() => new("all_rows");
public static TvfEntityBuilder<ITvfRow> ById(long id) => new("rows_by_id");
public static TvfEntityBuilder<ITvfRow> Between(long lo, long hi) => new("rows_between", "app");
```

Скалярные параметры фабрики объявляют только **позиции аргументов** SQL-вызова; имя, схема, клаузы,
индексы verbatim-аргументов и схема результата живут в дескрипторе. Источник всегда передаётся
**выражением**, поэтому узел вызова — а вместе с ним и аргументы — доходит до построителя SQL:

```csharp
var values = dataContext
    .From(() => Tvf.JsonEach("[1,2,3]"))
    .Select(r => r.Value)
    .ToList();
// 1, 2, 3
```

```sql
-- SQLite
select value
 from json_each('[1,2,3]')
-- SQL Server / PostgreSQL добавляют псевдоним
select value
 from json_each('[1,2,3]') as [t1]
```

Дескриптор не зависит от контекста: он не хранит `DataContext`, композируется без него и
привязывается к контексту только тогда, когда его потребляет `From`, соединение или APPLY. nextorm
только генерирует вызов — функция уже должна существовать в целевой базе данных.

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Value |
|-------|
| 1     |
| 2     |
| 3     |

### Ломающее изменение в 1.0.9-rc4

Прежняя статическая поверхность удалена **без shim-совместимости**:

* атрибут `[SqlTableFunction]` и методы-заглушки, возвращавшие `IQueryable<T>`, которые он помечал;
* точки входа табличной функции `From`/`CreateQueryBuilder…From`, принимавшие
  `Expression<Func<IQueryable<T>>>`.

Каждая табличная функция теперь — runtime-фабрика, описанная ниже. Миграция существующего объявления
заменяет атрибут и тело заглушки на дескриптор, а место вызова — на lambda-источник:

```csharp
// 1.0.9-rc4
public static TvfEntityBuilder<ITvfRow> ById(long id) => new("rows_by_id");

var rows = dataContext
    .From(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .ToList();
```

Метаданные `[SqlTableFunction]` (имя, схема, `WithClause`, `CallClause`, индексы verbatim-аргументов,
схема результата) переносятся в конструктор дескриптора без изменений; см.
[Объявление runtime-фабрики](#объявление-runtime-фабрики).

## Объявление runtime-фабрики

У дескриптора три конструктора: `TvfEntityBuilder(string name)`, `TvfEntityBuilder(string name,
string? schema)` и полный конструктор метаданных:

```csharp
public TvfEntityBuilder(
    string name,
    string? schema,
    string? withClause,
    string? callClause,
    IReadOnlyList<int> verbatimArguments,
    TableFunctionSchema resultSchema);
```

| Член | Во что рендерится |
|---|---|
| `Name` | имя SQL-функции |
| `Schema` | необязательный префикс `schema.name` |
| `WithClause` | завершающее тело `WITH (...)`, добавляемое после вызова |
| `CallClause` | verbatim SQL внутри скобок вызова, после аргументов |
| `VerbatimArguments` | индексы аргументов, выводимых без кавычек как SQL-идентификаторы, а не параметры |
| `ResultSchema` | куда рендерится объявленная вызывающим схема результата (см. [Схемы результата и ограничения провайдеров](13-tvf-result-schemas.md)) |

`name` обязателен и не может быть пустым; `verbatimArguments` никогда не `null` («нет verbatim-аргумента»
— это пустой список) и должен содержать различные неотрицательные индексы — пустое имя, отрицательный
или повторный индекс бросает `ArgumentException`, `null`-имя или `null`-список бросает
`ArgumentNullException`. Список защитно копируется, а метаданные задаются только конструктором, поэтому
последующее изменение коллекции вызывающего не может изменить дескриптор или уже построенный запрос.

Форма строки — обычная сущность, отображённая через `[Column]`, как любой другой DTO:

```csharp
public interface ITvfRow
{
    [Column("id")]
    long Id { get; set; }
    [Column("value")]
    string? Value { get; set; }
}

public interface IJsonEachRow
{
    [Column("key")]
    long Key { get; set; }
    [Column("value")]
    long Value { get; set; }
}
```

Остальные опции повторяют формы провайдеров, которые они порождают:

```csharp
public static class Tvf
{
    // со схемой: app.rows_between(a, b)
    public static TvfEntityBuilder<ITvfRow> Between(long lo, long hi) => new("rows_between", "app");

    // SQL Server OPENJSON ... WITH (...)
    public static TvfEntityBuilder<IOpenJsonTypedRow> OpenJsonTyped(string json) =>
        new("openjson", null, "name nvarchar(50) '$.name', age int '$.age'", null, [], TableFunctionSchema.None);

    // MySQL/MariaDB JSON_TABLE: схема находится внутри скобок
    public static TvfEntityBuilder<IJsonTableRow> JsonTable(string doc) =>
        new("json_table", null, null,
            ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')", [], TableFunctionSchema.None);

    // SQL Server CONTAINSTABLE: аргументы 0 и 1 — идентификаторы, а не значения
    public static TvfEntityBuilder<SqlFunctions.IKeyRankRow<int>> ContainsTable(string table, string column, string search) =>
        new("containstable", null, null, null, [0, 1], TableFunctionSchema.None);
}
```

Verbatim-аргумент, не являющийся константной строкой, или индекс, не указывающий на существующий
аргумент вызова фабрики, падает явно — он никогда не выводится как значение.

## Метаданные и чистота фабрики

Дескриптор материализуется из **узла вызова** фабрики, а не из значения:

* фабрика вызывается один раз с типизированным `default` в каждой позиции аргумента — только чтобы
  прочитать метаданные; выражения аргументов вызывающего для этого никогда не вычисляются;
* метаданные (имя, схема, клаузы, индексы verbatim-аргументов, схема результата) поэтому **не должны
  зависеть от значений аргументов**; фабрика — это объявление, а не интерпретатор языка выражений;
* захваченная конфигурация вне SQL-аргументов (статическое имя, константа схемы) допустима и читается
  при понижении источника, поэтому участвует в идентичности источника;
* количество вызовов фабрики не гарантируется, поэтому фабрика не должна иметь побочных эффектов;
* исходный вызов сохраняет свои аргументы: ни один аргумент не подменяется значением по умолчанию из
  проекции метаданных.

```csharp
// допустимо: аргумент задаёт только позицию, метаданные постоянны
public static TvfEntityBuilder<IRow> ById(long id) => new("rows_by_id");

// недопустимо: метаданные зависят от значения аргумента
public static TvfEntityBuilder<IRow> ById(long id) => new(id > 0 ? "rows_pos" : "rows_all");
```

## Аргументы остаются SQL-выражениями

Поскольку источник — lambda, каждый аргумент понижается обычным посетителем выражений: литерал
выводится как SQL-литерал, захват становится связанным параметром, `null`/`default` и вложенные
SQL-выражения сохраняются, а член внешней строки остаётся ссылкой на внешнюю колонку.

```csharp
var id = 5L;

var prepared = dataContext
    .From(() => Tvf.ById(id))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
-- плейсхолдер SQLite; SQL Server/PostgreSQL используют @id
select id
 from rows_by_id($id)
```

Имя со схемой и двумя аргументами рендерится как `app.rows_between(...)`:

```csharp
var lo = 1L;
var hi = 3L;

var prepared = dataContext
    .From(() => Tvf.Between(lo, hi))
    .Select(r => new { r.Id })
    .Prepare();
```

```sql
select id
 from app.rows_between($lo, $hi)
```

**Форма** аргументов входит в SQL-идентичность и в ключ плана, а **значение** — нет: изменённый захват
переиспользует кэшированный план, ничего не перерендеривает и обновляет только свой слот параметра,
тогда как литерал является частью текста инструкции, поэтому другой литерал — это другой план.

```csharp
var id = 1L;
QueryCommand<long> Build() => dataContext.From(() => Tvf.ById(id)).Select(r => r.Id);

var first = Build().Prepare();   // id = 1
id = 2;
var second = Build().Prepare();  // тот же кэшированный план, параметр обновлён на 2
```

Несколько источников обновляются независимо: если TVF стоит в `FROM`, а другая — источником
соединения, перепривязывается только изменённый захват, а второй слот сохраняет своё значение. Общая
команда `QueryCommand` контекста для этого не мутируется (никакого «залипающего» `Cache = false`),
поэтому кэш планов остаётся включённым для всех последующих запросов того же контекста.

## Запрос функции

`ctx.From(() => ...)` — единственная точка входа: lambda несёт вызов фабрики, а дескриптор, полученный
прямым вызовом (`ctx.From(Tvf.ById(5))`), этот вызов уже потерял — см.
[Несвязанные цепочки и захваченные дескрипторы](#несвязанные-цепочки-и-захваченные-дескрипторы).

Обычная цепочка построителя композируется внутри lambda и попадает в ту же инструкцию. Любая публичная
операция построителя, возвращающая построитель, с сохранёнными lambda- или константными аргументами
применяется заново в исходном порядке; вызов фабрики никогда не принимается за внешнюю операцию:

```csharp
var rows = dataContext
    .From(() => Tvf.JsonEach("[3,1,2]").Where(r => r.Value > 1).OrderByDescending(r => r.Value))
    .Select(r => r.Value)
    .ToList();
// 3, 2
```

```sql
-- SQLite
select value
 from json_each('[3,1,2]')
where (value > 1) order by value desc
```

Группировка и агрегаты работают так же:

```csharp
var rows = dataContext
    .From(() => Tvf.JsonEach("[1,1,2]"))
    .GroupBy(r => new { r.Value })
    .Select(g => new { g.Value, Cnt = SqlFunctions.Sql.count() })
    .ToList();
// (1, 2), (2, 1)
```

Вывод:

| Value | Cnt |
|-------|-----|
| 1     | 2   |
| 2     | 1   |

## Соединения

`Join`, `LeftJoin`, `RightJoin`, `FullJoin` и `CrossJoin` принимают runtime-источник как
`Expression<Func<EntityBuilder<T>>>` через extension-методы
[`TvfJoinSourceExtensions`](xref:NextORM.Core.TvfJoinSourceExtensions). Семейство покрывает generic,
non-generic и соединённые получатели арностей 2–8, с callback `JoinOptions` и без него:

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .Join(() => Tvf.AllRows(), (s, r) => r.Id == s.Id)
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQLite
select t1.id, t2.value
 from simple_entity as 't1'
 join all_rows() as 't2' on t2.id = cast(t1.id as bigint)
-- SQL Server / PostgreSQL отличаются только кавычками идентификаторов ([t2] / "t2")
```

`cast` — обычное объединение типов при соединении: `Tvf.AllRows().Id` имеет тип `long`, а
`ISimpleEntity.Id` — `int`. Поскольку это extension-методы, вызов, передающий сущность, CTE или
построитель производного запроса, по-прежнему связывается с существующими instance-перегрузками
без изменений.

## Применение функции

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) принимают коррелированную lambda, тело которой — вызов runtime-фабрики. Внешний параметр никогда не вычисляется в CLR: чтения его членов становятся ссылками на SQL-колонки, а вызов применяется по одному разу на строку левой стороны.

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .CrossApply(s => Tvf.ById(s.Id))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value
 from simple_entity as [t1]
 cross apply (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL
select t1.id, t3.value
 from simple_entity as "t1"
 cross join lateral (select t2.id, t2.value
 from rows_by_id(cast(t1.id as bigint)) as "t2") as "t3"
```

`OuterApply` — лево-сохраняющий двойник: он сохраняет строки левой стороны, для которых функция не
вернула строк, проецируя `null`.

**Коррелированный `JSON_TABLE`** особым образом обрабатывается в MySQL и MariaDB: функция
коррелируется в `FROM` неявно, поэтому рендерится прямо как источник соединения — без производной
обёртки и без ключевого слова `LATERAL` (MySQL отклоняет внешнюю ссылку внутри табличной функции под
`LATERAL`, а в MariaDB ключевого слова `LATERAL` нет вовсе):

```sql
-- MySQL / MariaDB: CROSS APPLY / OUTER APPLY
select t1.id, jt.child_id
 from parent as `t1`
 join json_table(t1.children_json, '$[*]' columns(child_id int path '$.child_id')) as jt
-- OUTER APPLY — это `left join json_table(...) as jt on true`
```

Некоррелированный источник, другая функция или источник, производный запрос которого фильтрует,
группирует, сортирует, разбивает на страницы или переименовывает колонки, сохраняет производный
рендеринг (`cross join lateral (select ... from fn(...) as t2) as t3`) на этих провайдерах.

Применение любого источника требует провайдера с поддержкой lateral — SQLite, ClickHouse и
in-memory-провайдер отклоняют коррелированный APPLY с `NotSupportedException`
([`SupportsApply`](xref:NextORM.Core.ISqlDialect.SupportsApply)). Применение некоррелированной функции
напрямую (`.CrossApply(() => Tvf.AllRows())`) на таком провайдере является обычным соединением и
отклоняется только там, где провайдер отклоняет саму форму соединения.

## Композиция, клонирование и повторное использование

Дескриптор — это построитель: `Where`, `OrderBy`, разбиение на страницы, соединения и меняющие тип
`Select`/`As` композируются на нём без контекста, и та же цепочка работает внутри lambda `From` (см.
[Запрос функции](#запрос-функции)). Тип строки фабрики и тип результата композиции отслеживаются
раздельно, поэтому проекция в другой DTO сохраняет источник и его аргументы:

```csharp
var builder = dataContext.From(() => Tvf.ById(id).As(row => new RowDto { Id = row.Id }));
```

То же относится к типизированной ссылке, клону и апкасту к `EntityBuilder<T>`: семантика TVF следует за
построителем, а не за статическим типом переменной. Дескриптор можно разделять между запросами и
контекстами; каждое потребление привязывает его независимо, и значения не протекают между контекстами.

### Несвязанные цепочки и захваченные дескрипторы

Дескриптор и его цепочка намеренно не связаны — placeholder-контекст не создаётся и никогда не
записывается в дескриптор:

```csharp
var descriptor = Tvf.ById(5);
descriptor.Where(r => r.Id > 0).ToCommand();
// InvalidOperationException: The builder composes an unbound table-valued-function source and carries
// no data context; consume it with ctx.From(...) before any context-dependent operation.
```

**Захваченный дескриптор** — переменная, хранящая результат вызова фабрики — отклоняется по имени,
потому что исходный узел вызова и, значит, происхождение аргументов потеряны:

```csharp
var descriptor = Tvf.ById(5);
var act = () => dataContext.From(() => descriptor);
// NotSupportedException: The source expression must be a direct call to a runtime
// table-valued-function factory, ... A captured descriptor loses the original argument provenance
// and cannot be lowered.
```

Любая другая форма отклоняется явно, а не угадывается: метод, не возвращающий `TvfEntityBuilder<T>`
("does not return TvfEntityBuilder<T>"), операция построителя вне поддерживаемого хребта композиции или
фабрика, вернувшая `null`.

## Встроенные табличные функции

Несколько распространённых табличных функций уже объявлены фабриками `TvfEntityBuilder<T>` на
`SqlFunctions.*`, поэтому пользовательская фабрика не нужна. Их поддержка гейтится по провайдеру
([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), в отличие
от пользовательской фабрики, которая выводится verbatim.

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` относятся к PostgreSQL (они возвращают [`SqlFunctions.IGenerateSeriesRow`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow)
с колонкой `generate_series` и [`SqlFunctions.IUnnestRow<T>`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1) с колонкой `unnest`):

```csharp
var numbers = dataContext
    .From(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

PostgreSQL также предоставляет set-returning функции для regexp, JSON и полнотекстового поиска:

| `SqlFunctions.Postgres.*` | Выходные колонки SQL | Форма строки |
| --- | --- | --- |
| `regexp_matches(source, pattern[, flags])` | `regexp_matches text[]` | `IRegexpMatchesRow` (`.Matches`) |
| `regexp_split_to_table(source, pattern[, flags])` | `regexp_split_to_table` | `IRegexpSplitToTableRow` (`.Value`) |
| `jsonb_array_elements(json)` / `jsonb_array_elements_text(json)` | `value` | `IJsonArrayElementsRow` (`.Value`) |
| `jsonb_each(json)` / `jsonb_each_text(json)` | `key`, `value` | `IJsonbEachRow` (`.Key`, `.Value`) |
| `jsonb_object_keys(json)` | `jsonb_object_keys` | `IJsonObjectKeysRow` (`.Key`) |
| `jsonb_path_query(json, jsonpath)` | `jsonb_path_query` | `IJsonPathQueryRow` (`.Value`) |
| `ts_stat(query)` | `word`, `ndoc`, `nentry` | `ITsStatRow` (`.Word`, `.Ndoc`, `.Nentry`) |
| `jsonb_to_record(json)` / `jsonb_to_recordset(json)` | объявляются `TRow` | `TRow` (см. [Динамическая схема результата](13-tvf-result-schemas.md#dynamic-result-schema)) |

```csharp
var json = JsonDocument.Parse("""{"a":1,"b":2}""");

var entries = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_each_text(json))
    .Select(r => new { r.Key, r.Value })
    .ToList();
// ("a", "1"), ("b", "2")
```

Операнд JSONPath передаётся текстом через `SqlFunctions.Postgres.jsonpath(path)`, который рендерит
`cast(path as jsonpath)`:

```csharp
var json = JsonDocument.Parse("""{"a":[1,2]}""");
var path = "$.a[*]";

var values = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_path_query(json, SqlFunctions.Postgres.jsonpath(path)))
    .Select(r => r.Value)
    .ToList();
// "1", "2"
```

PostgreSQL переименовывает единственную колонку скалярной set-returning функции (`generate_series`,
`unnest`, `regexp_matches`, `regexp_split_to_table`, `jsonb_object_keys`, `jsonb_path_query`) в псевдоним,
который nextorm добавляет производному источнику, поэтому диалект оборачивает такие вызовы в
одноколоночный подзапрос (`select generate_series from generate_series(...)`); функции с явной выходной
колонкой (`value`, `key`/`value`, `word`/`ndoc`/`nentry`) выводятся без изменений.

`SqlFunctions.SqlServer.string_split` — SQL Server 2016+ и возвращает [`SqlFunctions.IStringSplitRow`](xref:NextORM.Core.SqlFunctions.IStringSplitRow) (единственная
колонка `value`). Порядок фрагментов не гарантируется, поэтому добавляйте `order by`, когда важен
порядок входа:

```csharp
var csv = "a,b,c";
var separator = ",";

var fragments = dataContext
    .From(() => SqlFunctions.SqlServer.string_split(csv, separator))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value
 from string_split(@csv, @separator) as [t1]
```

`SqlFunctions.SqlServer.openjson` — SQL Server 2016+ и возвращает [`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`key`/`value`/`type`).
Схема по умолчанию даёт свойства JSON-объекта или элементы JSON-массива:

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .From(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type
 from openjson(@json) as [t1]
```

Для **типизированной схемы** (`OPENJSON ... WITH (...)`) объявите собственную фабрику, форма строки
которой соответствует схеме, и передайте тело клаузы как `withClause`; тело выводится verbatim после
вызова:

```csharp
public interface IOpenJsonTypedRow
{
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public static class OpenJsonTvf
{
    public static TvfEntityBuilder<IOpenJsonTypedRow> Typed(string json) =>
        new("openjson", null, "name nvarchar(50) '$.name', age int '$.age'");
}

var person = dataContext
    .From(() => OpenJsonTvf.Typed("""{"name":"Ada","age":36}"""))
    .Select(r => new { r.Name, r.Age })
    .First();
```

```sql
select name, age
 from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]
```

SQL Server также предоставляет табличные функции полнотекстового поиска `SqlFunctions.SqlServer.containstable` и
`freetexttable`, которые отдают ключ совпавшей строки и оценку релевантности через
[`SqlFunctions.IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1) (`.Key`, `.Rank`). Соедините
функцию обратно с индексированной таблицей по `Key` и отсортируйте по `Rank`:

```csharp
var search = "stuffed bear";

var ranked = dataContext
    .From(() => SqlFunctions.SqlServer.containstable<int>("documents", "title", search))
    .Join<IDocument>((k, d) => k.Key == d.Id)
    .OrderByDescending(p => p.Item1.Rank)
    .Select(p => new { p.Item2.Id, p.Item1.Rank })
    .ToList();
```

`containstable` использует `CONTAINSTABLE` (синтаксис boolean/prefix/phrase), а `freetexttable` —
естественно-языковой `FREETEXTTABLE`; обе только для SQL Server
([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), остальные провайдеры бросают
`NotSupportedException`. Аргументы `table` и `column` выводятся **verbatim** как идентификаторы
(объявлены через `VerbatimArguments`), поэтому передавайте имя таблицы или псевдоним точно в том виде,
в каком он появляется в сгенерированном запросе, и только доверенные значения.

Для табличной функции, схема которой находится **внутри** скобок вызова, а не в завершающей клаузе
`WITH`, передайте `callClause`, чтобы добавить verbatim SQL после аргументов (включите собственный
ведущий разделитель). Типичный случай — MySQL `JSON_TABLE`:

```csharp
public interface IJsonTableRow
{
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

public static class JsonTableTvf
{
    public static TvfEntityBuilder<IJsonTableRow> JsonTable(string doc) =>
        new("json_table", null, null,
            ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')");
}
```

```sql
select id, name
 from json_table(@doc, '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')) as `t1`
```

`VerbatimArguments` — родственный хук для функций, принимающих сырой идентификатор (имя таблицы или
колонки): каждый перечисленный аргумент должен быть константной строкой и выводится без кавычек.

`SqlFunctions.Sqlite.json_each`/`json_tree` — табличные функции JSON в SQLite, возвращающие
[`SqlFunctions.IJsonEachRow`](xref:NextORM.Core.SqlFunctions.IJsonEachRow)/[`SqlFunctions.IJsonTreeRow`](xref:NextORM.Core.SqlFunctions.IJsonTreeRow) (`key`, `value`, `type`, `fullkey`, `path`,
плюс `id`/`parent` для `json_tree`); источник поиска FTS5 — `SqlFunctions.Sqlite.MatchTable<TEntity>`.
См. [SQLite](provider-specific-sql/sqlite.md#json_each--json_tree-table-functions).

`SqlFunctions.ClickHouse.numbers`/`numbers_mt` — табличные функции ClickHouse, возвращающие
[`SqlFunctions.INumbersRow`](xref:NextORM.Core.SqlFunctions.INumbersRow) (единственная колонка `number`). `numbers(count)` даёт
последовательные целые от нуля, `numbers(start, stop[, step])` — произвольный диапазон:

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.numbers(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select number as `Value`
 from (select toInt64(number) as number
 from numbers(@count)) as `t1`
```

Колонка `number` имеет тип `UInt64`, который читатель строк не может материализовать, поэтому диалект
оборачивает вызов в приводящий подзапрос (`toInt64(number) as number`).

`SqlFunctions.ClickHouse.zeros`/`zeros_mt` — табличные функции ClickHouse, возвращающие количество строк
[`SqlFunctions.IZerosRow`](xref:NextORM.Core.SqlFunctions.IZerosRow) (единственная колонка `zero UInt8`):

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

```sql
select zero as `Value`
 from zeros(@count) as `t1`
```

В отличие от `numbers`, колонка `zero` имеет тип `UInt8`, который читатель строк материализует напрямую
как `byte`, поэтому обёртка-приведение не нужна.

`SqlFunctions.ClickHouse.generate_random`/`generate_random(seed)` — табличные функции тестовых данных
ClickHouse, возвращающие [`SqlFunctions.IGenerateRandomRow`](xref:NextORM.Core.SqlFunctions.IGenerateRandomRow)
(`id UInt64`, `value Float64`, `name String`). Собственная схема ClickHouse — строковый аргумент, а у
формы без аргументов схема случайна, поэтому встроенный помощник фиксирует структуру, а его колонка
`id` приводится к `Int64` тем же оборачивающим подзапросом, что и `numbers`; поток неограничен, поэтому
добавьте страницу/лимит:

```csharp
var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.generate_random())
    .Page(3, 0)
    .Select(r => new { r.Id, r.Value, r.Name })
    .ToList();
```

```sql
select id as `Id`, value as `Value`, name as `Name`
 from (select toInt64(id) as id, value, name
 from generateRandom('id UInt64, value Float64, name String')) as `t1`
limit 3
```

ClickHouse также предоставляет серверные/кластерные табличные функции, предобъявленные как generic-фабрики,
форма строки которых объявляется вызывающим. Имена `[Column]` интерфейса `TRow` должны совпадать с
аргументом `structure` (`url`/`s3`/`file`) или с целевой таблицей (`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`):

| `SqlFunctions.ClickHouse.*` | Выходной SQL |
| --- | --- |
| `url<TRow>(url, format, structure)` | `url(url, format, structure)` |
| `s3<TRow>(url, format, structure)` | `s3(url, format, structure)` |
| `file<TRow>(path, format, structure)` | `file(path, format, structure)` |
| `remote<TRow>(addresses, database, table)` | `remote(addresses, database, table)` |
| `remote_secure<TRow>(addresses, database, table)` | `remoteSecure(addresses, database, table)` |
| `cluster<TRow>(cluster, database, table)` | `cluster(cluster, database, table)` |
| `cluster_all_replicas<TRow>(cluster, database, table)` | `clusterAllReplicas(cluster, database, table)` |
| `values<TRow>(tuples)` | `values('<structure from TRow>', tuples...)` |

```csharp
public interface IHitsRow
{
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

var hits = dataContext
    .From(() => SqlFunctions.ClickHouse.url<IHitsRow>(
        "http://127.0.0.1:12345/", "CSV", "id UInt64, name String"))
    .Select(r => new { r.Id, r.Name })
    .ToList();
```

```sql
select id as `Id`, name as `Name`
 from url(@url, @format, @structure) as `t1`
```

Функции только для ClickHouse ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)))
и требуют соответствующих серверных прав; аутентификация URL/S3/remote — ответственность сервера,
поэтому предпочитайте именованные коллекции или `<remote_servers>`, чтобы держать секреты вне запроса и
его плана. Табличные функции `format`/`merge`/`input` намеренно **не** предобъявлены — схема `format`
может выводиться из данных, `merge` берёт её из нижележащих таблиц, а `input` допустима только в
INSERT, — поэтому объявите собственную фабрику или используйте
[`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object))
для них.

Отображённая функция должна существовать в базе данных — nextorm только генерирует вызов, он не создаёт
функцию, — поэтому используйте помощник только на провайдере, который её определяет. Встроенные
помощники гейтятся [`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)):
PostgreSQL включает `generate_series`, `unnest`, `regexp_matches`, `regexp_split_to_table`,
`jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`, `ts_stat` и
record-функции `jsonb_to_record`/`jsonb_to_recordset`; SQL Server включает `string_split`/`openjson`,
ClickHouse включает `numbers`/`numbers_mt`, `zeros`/`zeros_mt`, `generateRandom`, `values` и
серверные/кластерные функции `url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`,
SQLite включает `json_each`/`json_tree`/`fts5`, а любой другой провайдер отклоняет их с
`NotSupportedException`. Пользовательская фабрика никогда не гейтится, но объявленная вызывающим
`ResultSchema` гейтится
[`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema)).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Вызов функции выводится без псевдонима для простого источника; псевдоним всё равно выводится, когда источник соединён. Коррелированный APPLY отклоняется. |
| SQL Server | Производный источник получает псевдоним (`as [t1]`); коррелированный APPLY рендерится как `cross apply`/`outer apply`. |
| PostgreSQL | Псевдоним обязателен и всегда выводится (`as "t1"`); коррелированный APPLY рендерится как `cross join lateral`/`left join lateral`. |
| MySQL | Производный источник получает псевдоним (`` as `t1` ``); коррелированный `JSON_TABLE` сам является источником соединения, без `LATERAL`. |
| MariaDB | Как MySQL; в диалекте вообще нет ключевого слова `LATERAL`. |
| ClickHouse | Производный источник получает псевдоним (`` as `t1` ``); коррелированный APPLY отклоняется. |
| In-memory | Источники табличных функций **не поддерживаются** (`NotSupportedException`: "Table-valued function sources are not supported by the in-memory provider."). |

Формы схемы результата, которые может рендерить каждый провайдер, перечислены в
[Схемы результата и ограничения провайдеров](13-tvf-result-schemas.md#ограничения-провайдеров).

> Интеграционные тесты используют встроенную в SQLite `json_each`, поэтому общий набор тестов
> прогоняет поведенческие тесты TVF только на SQLite; SQL Server и PostgreSQL пропускаются через
> `ITestProvider.SupportsTableValuedFunctions`, потому что функцию нужно сначала создать в базе данных.
> Генерация SQL для всех провайдеров всё равно покрыта тестами `TableFunction_*` и `T320`.

## См. также

* [Соединения](04-joins.md) — соединение TVF с таблицей или другой TVF и её применение через `CrossApply`/`OuterApply` ([APPLY и LATERAL](04-joins.md#apply-and-lateral)).
* [Схемы результата и ограничения провайдеров](13-tvf-result-schemas.md) — объявленные вызывающим выходные колонки и формы по провайдерам.
* [Пользовательские функции](12-user-defined-functions.md) — скалярный эквивалент.
* [Инструкция INSERT](17-insert-statement.md) — TVF (`unnest`, `generate_series`) как источник `INSERT ... SELECT`.
* [Обзор провайдеров](../connecting-providers/overview.md) — требование псевдонима TVF по провайдерам.

---

Source: `src/nextorm.core/Builders/TvfEntityBuilder.cs`, `src/nextorm.core/Builders/SourceExpressionLowerer.cs`, `src/nextorm.core/Builders/TvfJoinSourceExtensions.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs`;
`tests/nextorm.core.tests/Builders/T320RuntimeTvfTests.cs`, `tests/nextorm.core.tests/Builders/T320RuntimeTvfIdentityTests.cs`;
`tests/nextorm.integration.tests/T320/T320MariaDbTvfTests.cs`;
сгенерированный SQL: `tests/nextorm.sqlite.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.sqlserver.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.postgres.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.mysql.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.clickhouse.tests/T320/T320RuntimeTvfSqlTests.cs`.
