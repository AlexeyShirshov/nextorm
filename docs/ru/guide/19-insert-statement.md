# Изменение данных (INSERT)

> В nextorm появилась небольшая, но явная поверхность DML: [`InsertInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.InsertInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) строит параметризованный `INSERT ... VALUES` и возвращает число затронутых строк, сгенерированный ключ или вставленные строки. Тут нет ни change tracking, ни `SaveChanges`: каждый терминал выполняет ровно одну команду.

**Что нужно знать:** [Запросы и проекции](01-querying-and-projections.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Обзор провайдеров](../providers/overview.md)

## Обзор

nextorm всегда был **конструктором запросов**; запись строки — та же идея, применённая к `INSERT`.
Билдер собирает целевую таблицу и колонки, активный диалект рендерит утверждение (параметризуя каждое
значение так же, как в запросе), а контекст выполняет его один раз:

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

var affected = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Id, 1)
    .Value(x => x.Name, "a")
    .Insert();
```

Здесь сознательно **нет** unit of work, трекера изменений и графа сущностей: это явная модель команд
как в linq2db/`ExecuteNonQuery`, а не `SaveChanges` из EF Core. См.
[Ограничения и что вне области](../advanced/limitations.md).

## Объявление ключа, identity и computed-колонок

Билдер вставки использует метаданные сущности, поэтому ему нужно знать, какие колонки генерирует БД.
Объявляйте их стандартными атрибутами (на интерфейсе, если сущность маппится через интерфейс) или
флюентно на билдере метаданных:

| Понятие | Атрибут | Флюентно |
|---|---|---|
| Ключ сущности | `[Key]` | `Property(x => x.Id).Key()` |
| Identity / auto-increment | `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` | `Property(x => x.Id).Identity()` |
| Computed (никогда не пишется) | `[DatabaseGenerated(DatabaseGeneratedOption.Computed)]` | `Property(x => x.Total).Computed()` |

```csharp
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}
```

Либо объявите то же отображение флюентно на обычном POCO, вовсе без атрибутов - делегат передаётся в
`InsertInto<T>` (тот же `cfg` можно передать в `From<T>`, чтобы зарегистрировать отображение заранее):

```csharp
public class Order            // без атрибутов
{
    public long Id { get; set; }
    public decimal Total { get; set; }
    public decimal TotalWithVat { get; set; }
}

ctx.InsertInto<Order>(cfg =>
{
    cfg.Table("orders");
    cfg.Property(x => x.Id).HasColumnName("id").Key().Identity();
    cfg.Property(x => x.Total).HasColumnName("total");
    cfg.Property(x => x.TotalWithVat).HasColumnName("total_with_vat").Computed();
});
```

Если ни одно свойство не объявлено через `[Key]`, метаданные выводят ключ по соглашению `Id` или
`<TypeName>Id`. `Identity` и `Computed` **не** выводятся: объявляйте их явно, иначе они считаются
обычными записываемыми колонками.

## Запись значений

Каждое значение привязывается параметром. Доступно несколько форм; внутри одного билдера они взаимоисключающи.

**По колонкам** (одна строка). Значение — CLR-значение:

```csharp
ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Id, 1)
    .Value(x => x.Name, "a")
    .Insert();
```

Значением может быть и другая mapped-колонка той же сущности — тогда рендерится ссылка на колонку,
а не параметр:

```csharp
ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, x => x.OtherName)
    .Insert();
```

Здесь принимается только обращение к mapped-свойству (или выражение без параметров); иначе бросается
`NotSupportedException`.

**Целой сущностью.** Пишутся все mapped-записываемые колонки; колонки `Identity` и `Computed`
исключаются автоматически:

```csharp
ctx.InsertInto<ISimpleEntity>().Values(entity).Insert();
```

**Батчем сущностей.** То же самое, повторённое для последовательности:

```csharp
ctx.InsertInto<ISimpleEntity>().Values(new[] { e1, e2, e3 }).Insert();
```

**Батч из источника.** `Values(source, mapping)` пишет по строке на элемент, а проекция выбирает колонки.
Конкретная сущность — через инициализатор объекта, интерфейс — через анонимный тип; имя каждого члена
должно совпадать со свойством сущности:

```csharp
ctx.InsertInto<Order>()
    .Values(dtos, d => new Order { Total = d.Total })
    .Insert();

ctx.InsertInto<ISimpleEntity>()
    .Values(dtos, d => new { Name = d.Name })
    .Insert();
```

**Батч из запроса (на сервере).** Та же подпись `Values(source, mapping)` принимает и
`EntityBuilder<TSource>` — тогда рендерится `INSERT ... SELECT`, и строки читает сама БД, а не клиент
(удобно для копирования или серверной фильтрации). Проекция выбирает записываемые колонки так же, как
выше:

```csharp
ctx.InsertInto<IOrder>()
    .Values(ctx.From<OrderDto>().Where(d => d.Active), d => new { d.Id, d.Amount })
    .Insert();
```

Источник — обычный билдер запроса (`Where`/`Join`/`GroupBy`/`OrderBy`/...), его параметры переносятся в
`INSERT`. Источник с CTE (`With`) поддерживается: клоз `WITH` поднимается перед `INSERT`
(`with c as (...) insert into ... select ... from c`), так как модифицирующий CTE обязан быть на верхнем
уровне. Рантайм-плейсхолдер `SqlFunctions.Parameter` не поддерживается, а `DEFAULT` недопустим как
выбираемое значение — оба бросают `NotSupportedException`.
`Returning`/`ReturningIdentity`/`ReturningKey` сочетаются с ним там, где это умеет провайдер, а
`.Single()` бросает, если запрос записывает больше одной строки. Правила целевых колонок (имена членов,
запрет `Computed`-цели) — те же, что у клиентской проекции.

**Источник-CTE** — допустимый серверный источник: постройте scope через `With`, прочитайте CTE по имени и
передайте полученный `EntityBuilder<TableAlias>` в `Values`. Колонки CTE читаются по имени, поэтому маппинг
должен использовать выходные алиасы тела CTE:

```csharp
var source = ctx
    .With("recent", ctx.From<ISimpleEntity>()
        .Where(s => s.Id > 1)
        .Select(s => new { s.Id }))
    .From("recent");

ctx.InsertInto<IOrder>()
    .Values(source, t => new { CustomerId = t.GetInt32("id") })
    .Insert();
// with recent as (select id from simple_entity where (id > 1))
// insert into "order" (customer_id) select id from recent as "t1"
```

В PostgreSQL источником может быть даже модифицирующий CTE (`ctx.With(имя, insert.Returning(...))`); см.
[Модифицирующий CTE (PostgreSQL)](#модифицирующий-cte-postgresql).

**Табличная функция** тоже годится как источник: `FromTableFunction` даёт `EntityBuilder<TSource>`,
поэтому `unnest`/`generate_series` могут питать вставку. Массив — настоящий параметр, поэтому
передавайте его захваченной переменной: рантайм-плейсхолдер `SqlFunctions.Parameter` отклоняется (см.
выше):

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.InsertInto<IdRow>()
    .Values(ctx.FromTableFunction(() => SqlFunctions.Postgres.unnest(ids)), r => new { r.Value })
    .Insert();
// insert into id_row (value) select unnest as "Value" from (select unnest from unnest(@ids)) as "t1"
```

`unnest` и `generate_series` есть только в PostgreSQL.

**Скалярная колонка.** Когда у сущности ровно одна записываемая колонка (остальные — `Identity`/
`Computed`), значение или значения передаются напрямую, без селектора и проекции:

```csharp
ctx.InsertInto<ISimpleEntity>().Value("a").Insert();                      // одна строка
ctx.InsertInto<ISimpleEntity>().Values(new[] { "a", "b", "c" }).Insert(); // три строки
```

Скалярной форме нужна **ровно одна** записываемая колонка: если их несколько — используйте проекцию или
укажите колонку явно (скалярный вызов в обоих случаях бросает `InvalidOperationException`). Если
записываемых колонок нет (все `Identity`/`Computed`) — передавать нечего: строка целиком из дефолтов
вставляется через `ctx.InsertInto<T>().Insert()` (см. [Дефолты](#запись-значений)).

**Батч по колонкам.** Для колоночно-ориентированных данных — по одному вызову на колонку; все колонки
обязаны дать одинаковое число значений:

```csharp
ctx.InsertInto<Product>()
    .Values(x => x.Name, new[] { "a", "b" })
    .Values(x => x.Price, new[] { 1m, 2m })
    .Insert();
```

Запись в колонку `Computed` всегда бросает `NotSupportedException`; колонку `Identity` можно указать
явно (тогда её значение берётся из ввода, а не генерируется).

**Дефолты.** Значением может быть `DEFAULT`-значение колонки, а сущность, у которой все колонки
сгенерированы (`Identity`/`Computed`), не требует значений вообще — вставляется одна строка целиком
из дефолтов:

```csharp
ctx.InsertInto<ISimpleEntity>().Value(x => x.Name, SqlDefault.Value).Insert();

// все колонки — Identity/Computed:
ctx.InsertInto<AuditRow>().Insert();
```

Строка «только дефолты» использует нативную форму провайдера (`DEFAULT VALUES` у PostgreSQL,
SQL Server и SQLite; `() VALUES ()` у MySQL/MariaDB). Запись `DEFAULT` как значения не выражается
в SQLite (вместо этого опустите колонку — применится тот же дефолт), а ClickHouse не поддерживает
ни одну из форм; в обоих случаях бросается `NotSupportedException`. `SqlDefault.Value` также
работает как член проекции в `Values(source, mapping)`.

## Модифицирующий CTE (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер, принимающий модифицирующую инструкцию как тело CTE
(`WITH <имя> AS (INSERT ... RETURNING ...)`); гейтится
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). Начните scope с
перегрузки `With(имя, insert)`: она принимает returning-insert и возвращает
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1), типизированный по проекции
`RETURNING`. Возвращённые строки читаются через `From(имя)` (типизированно):

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("ins")
    .Where(r => r.Total > 0)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
-- PostgreSQL
with ins as (insert into orders (customer_id) values (@p0) returning id, total) select id from ins as "t1"
 where (t1.total > 0)
```

`From(имя)` читает `RETURNING`-колонки мутации полным набором операторов — фильтр, join, агрегат,
сортировка и пагинация — а проекция может их переименовывать (`Returning(x => new { x.Total })` читается
обратно как `r.Total`). [`FromTable`](xref:NextORM.Core.MutationCteQuery`1.FromTable(System.String)) читает
соседний read-CTE как обычный источник [`TableAlias`](xref:NextORM.Core.TableAlias):

```csharp
var rows = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.Id }))
    .With("src", dataContext.From<IOrder>().Select(x => new { x.Total }))
    .FromTable("src")
    .Select(t => new { total = t["Total"].AsInt })
    .ToList();
```

> Строки, вставленные модифицирующим CTE, **не** видны другим CTE той же инструкции: PostgreSQL
> выполняет под-инструкции параллельно на одном снимке. Читайте их через `From(имя)` или в отдельной
> инструкции.

Тело может быть `VALUES`-insert или `INSERT ... SELECT` (`Values(source, mapping)` сочетается с
`Returning`); объявленные источником CTE отбрасываются, так как уже объявлены внешним `WITH`. Объявляйте
read-CTE первыми, чтобы мутация могла на них ссылаться: `CteQuery.With(имя, insert)` добавляет
модифицирующий CTE **после** уже собранных read-CTE, поэтому его тело может их читать:

```csharp
var scope = dataContext.With("src",
    dataContext.From<ICustomer>().Where(c => c.Active).Select(c => new { c.Id }));

var rows = scope
    .With("ins", dataContext.InsertInto<IOrder>()
        .Values(scope.From("src"), a => new { CustomerId = a.GetInt32("Id") })
        .Returning(x => new { x.Id }))
    .From("ins")
    .Select(r => new { r.Id })
    .ToList();
```

Модифицирующий CTE может также питать главный `INSERT ... SELECT`: `WITH` поднимается перед `INSERT`
(PostgreSQL требует модифицирующую инструкцию на верхнем уровне):

```csharp
var source = dataContext
    .With("ins", dataContext.InsertInto<IOrder>()
        .Value(x => x.CustomerId, 7)
        .Returning(x => new { x.CustomerId }))
    .From("ins");

dataContext.InsertInto<IOrder>()
    .Values(source, r => new { r.CustomerId })
    .Insert();
```

Инструкция, в `WITH` которой есть модифицирующий CTE, никогда не попадает в кэш планов (она имеет
побочный эффект). Остальные провайдеры отклоняют `With(имя, insert)` с `NotSupportedException`, так как их
тело CTE обязано быть `SELECT`. Общие (read) CTE — в [Общих табличных выражениях](09-cte.md); поверхность `UPDATE` описана в [Изменении данных (UPDATE)](21-update-statement.md).

## Получение сгенерированного ключа

Терминалы чтения ключа возвращают [`InsertReturningBuilder<TEntity,TResult>`](xref:NextORM.Core.InsertReturningBuilder`2)
и читаются через его терминалы `Single()`/`ToList()` (см. [Возврат вставленных строк](#возврат-вставленных-строк)):

| Терминал | Что выбирает | Требует |
|---|---|---|
| `ReturningIdentity(x => x.Id)` | колонку, указанную селектором | колонка объявлена `Identity`; у провайдера есть `RETURNING`/`OUTPUT` |
| `ReturningIdentity<long>()` | скалярную identity-функцию провайдера | `ISqlDialect.SupportsIdentityFunction` |
| `ReturningKey<long>()` | ключевую колонку из метаданных | у сущности есть ключ; `RETURNING`/`OUTPUT` или фолбэк на identity-функцию |

Используется нативная форма провайдера:

| Провайдер | Селектор колонки / ключ | Identity-функция |
|---|---|---|
| SQLite | `INSERT ... RETURNING id` | `SELECT last_insert_rowid()` |
| PostgreSQL | `INSERT ... RETURNING id` | `SELECT lastval()` |
| SQL Server | `OUTPUT inserted.id` | `SELECT SCOPE_IDENTITY()` (тот же батч, что и вставка) |
| MySQL / MariaDB | фолбэк `SELECT LAST_INSERT_ID()` | `SELECT LAST_INSERT_ID()` |
| ClickHouse | невыразимо | невыразимо |
| In-memory | невыразимо | невыразимо (только чтение) |

```csharp
long fromColumn = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningIdentity(x => x.Id)
    .Single();

long fromFunction = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningIdentity<long>()
    .Single();

long fromMetadata = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .ReturningKey<long>()
    .Single();
```

* `ReturningIdentity(selector)` требует, чтобы выбранная колонка была объявлена `Identity`; `ReturningKey<TKey>()`
  резолвит ключ из метаданных (`[Key]`, `.Key()` или конвенция `Id`/`<Type>Id`) и отклоняет сущность без ключа
  или с несколькими ключами.
* Там, где у провайдера нет `RETURNING`/`OUTPUT` (MySQL/MariaDB), identity-колонка откатывается на
  `LAST_INSERT_ID()`; не-identity ключ отклоняется `NotSupportedException`, а не возвращает неверное значение.
* `ReturningIdentity<TKey>()` не называет колонку: она дописывает identity-функцию провайдера к вставке в том же
  батче, поэтому на SQL Server `SCOPE_IDENTITY()` остаётся корректным при триггерах. Там предпочитайте селекторную
  форму.
* ClickHouse и in-memory провайдер бросают `NotSupportedException`.

## Возврат вставленных строк

Помимо одного сгенерированного ключа `Returning()` материализует строки, которые реально записала БД -
включая identity- и computed-колонки - через нативную форму `RETURNING`/`OUTPUT` провайдера, без
второго `SELECT`:

```csharp
var row = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning()
    .Single();                        // Order с сгенерированным Id

var projected = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning(x => new { x.Id, x.Name })
    .Single();                        // анонимный { Id, Name }

var rows = ctx.InsertInto<Order>()
    .Values([o1, o2])
    .Returning()
    .ToList();                        // IReadOnlyList<Order>, по элементу на строку
```

* `Returning()` возвращает всю mapped-сущность; `Returning(projection)` - спроецированную форму.
  Проекция может быть identity, одним mapped-свойством, анонимным типом, позиционным конструктором
  или member-init и может ссылаться только на mapped-свойства.
* Терминалы: `Single()`/`SingleAsync()` рассчитаны на одну строку и бросают
  `InvalidOperationException` на батче; `ToList()`/`ToListAsync()` работают
  и для одной, и для батча и возвращают `IReadOnlyList<TResult>`.
* Строки материализуются тем же конвейером проекций, что и запрос: форма сущности возвращает сущность,
  форма проекции - её форму.
* `ToSql()` рендерит statement - включая список `RETURNING`/`OUTPUT` - без выполнения.

| Провайдер | Форма | Поддержка |
|---|---|---|
| SQLite | `INSERT ... RETURNING <cols>` (3.35.0+) | да |
| PostgreSQL | `INSERT ... RETURNING <cols>` | да |
| SQL Server | `INSERT ... OUTPUT inserted.<cols>` (между списком колонок и `VALUES`) | да |
| MySQL | — | `NotSupportedException` |
| MariaDB | — (диалект на базе MySQL) | `NotSupportedException` |
| ClickHouse | — | `NotSupportedException` |
| In-memory | — (только чтение) | `NotSupportedException` |

**Порядок.** Строки приходят в порядке result-set, но ни один провайдер не гарантирует, что этот
порядок совпадает с порядком входных строк при мульти-вставке (SQLite документирует порядок как
произвольный). Считайте `ToList()` неупорядоченным множеством и не полагайтесь на
соответствие `rows[i]` входу `i`.

**Вся сущность и интерфейс.** `Returning()` требует конкретный `TEntity` для материализации. Для
сущности, отображённой через интерфейс, либо проецируйте колонки
(`Returning(x => new { x.Id, x.Name })`), либо используйте класс, реализующий интерфейс;
`Returning()` на самом интерфейсе бросает `NotSupportedException` при исполнении.

## Запись изменённых строк в таблицу (`OUTPUT INTO`)

SQL Server умеет записывать изменённые строки в существующую таблицу вместо (или в дополнение к)
возврату клиенту, через форму `OUTPUT ... INTO <target>(columns)`. Начните с returning-билдера и
вызовите `OutputInto(targetTable)`:

```csharp
// записать вставленные строки в audit_log; клиенту ничего не возвращается
var written = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning(x => new { x.Id, x.Name })
    .OutputInto("audit_log")
    .Execute();                       // int: число затронутых строк

// записать в audit_log и вернуть те же строки клиенту (второй OUTPUT)
var rows = ctx.InsertInto<Order>()
    .Value(x => x.Name, "a")
    .Returning(x => new { x.Id, x.Name })
    .OutputIntoThenOutput("audit_log")
    .ToList();                        // IReadOnlyList<{ Id, Name }>
```

* `OutputInto(...)` возвращает [`OutputIntoBuilder`](xref:NextORM.Core.OutputIntoBuilder),
  у которого единственные терминалы — `Execute()`/`ExecuteAsync()` (число затронутых строк) и `ToSql()`.
  Строковых терминалов нет, так как клиенту ничего не приходит.
* `OutputIntoThenOutput(...)` возвращает обычный returning-билдер, поэтому `Single()`/`ToList()` работают:
  инструкция несёт и клаузу `INTO`, и клиентский `OUTPUT`.
* Цель — явное имя таблицы: nextorm не объявляет табличную переменную (`DECLARE @t TABLE ...`) в этой фазе.
  Целевая таблица должна уже существовать, а её колонки — иметь те же имена, что и выбранные выходные
  колонки (список выбранных колонок переиспользуется как список колонок цели); имя цели и колонки
  квотируются как любые другие идентификаторы.
* Те же два метода доступны на returning-билдерах `UPDATE` и `DELETE` (для `DELETE` удалённая строка
  читается через алиас `deleted`). Пустое имя цели или форма identity-функции (`ReturningIdentity<TKey>()`,
  не выбирающая колонку) бросают исключение.
* Только SQL Server реализует `ISqlDialect.SupportsOutputInto`; остальные провайдеры бросают
  `NotSupportedException` при рендере инструкции.

## Массовая вставка (bulk)

Запись целого набора — нативные bulk-пути, чанкинг, `Returning`, `IgnoreDuplicates` и `KeepIdentity` —
описана в отдельном гайде: [Массовая вставка](24-bulk-insert.md).

## Upsert и MERGE

Тот же билдер пишет строки и через `MERGE`: переносимый **key upsert** (`OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`, рендерится как `INSERT ... ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`) и общий **полный `MERGE`** с ветками `WHEN MATCHED`/`WHEN NOT MATCHED`, произвольными условиями, `THEN DELETE`/`THEN DO NOTHING` и `RETURNING`/`OUTPUT`. Оба стартуют с `ctx.MergeInto<TEntity>()` и описаны в отдельном гайде:

- [Слияние данных (MERGE / upsert)](23-merge-statement.md)

## Просмотр SQL

[`ToSql()`](xref:NextORM.Core.InsertBuilder`1.ToSql) рендерит параметризованный SQL, который выполнил
бы обычный `Insert()`, не открывая соединение. Полезно для диагностики и SQL-generation тестов.

```csharp
var sql = ctx.InsertInto<ISimpleEntity>().Value(x => x.Name, "a").ToSql();
// insert into simple_entity (name) values (@p0)
```

## Сводка по провайдерам

| Провайдер | `INSERT ... VALUES` | Сгенерированный ключ | Возврат строк | Примечание |
|---|---|---|---|---|
| SQLite | да | `RETURNING` (а также `last_insert_rowid()`) | да | |
| PostgreSQL | да | `RETURNING` | да | |
| SQL Server | да | `OUTPUT inserted.<col>` | да | |
| MySQL | да | `LAST_INSERT_ID()` | — | общего `RETURNING` нет |
| MariaDB | да | `LAST_INSERT_ID()` | — | `RETURNING` (10.5+) не используется |
| ClickHouse | да (малые батчи) | — | — | массовая вставка — портируемый `INSERT ... VALUES`; ограничивайте `MaxBatchSize` |
| In-memory | — | — | — | контекст только для чтения; `NotSupportedException` |

Сам `INSERT ... VALUES` кросс-провайдерный и не гейтится: одно и то же API `InsertInto<T>()` работает
на всех SQL-провайдерах. Отличается лишь форма возврата ключа, и провайдер, который её не выражает,
отклоняет `ReturningIdentity`/`ReturningKey` через `NotSupportedException`, а не генерирует некорректный SQL.
SQL Server дополнительно записывает изменённые строки в существующую таблицу через `OUTPUT INTO`; см.
[Запись изменённых строк в таблицу](#запись-изменённых-строк-в-таблицу-output-into).

## Примечания и ограничения фазы 1

* **Значения — параметры.** Константа, переданная в `Value(...)`, никогда не встраивается в SQL; она
  становится именованным параметром (`@p0`, `$p0`, ...), привязываемым при выполнении.
* **Перегрузки с `null`.** `Value(x => x.Name, null)` неоднозначен между перегрузкой значения и
  перегрузкой выражения-колонки; приведите литерал: `Value(x => x.Name, (string?)null)`.
* **Key upsert и полный `MERGE`** — key upsert через `ON CONFLICT`/`ON DUPLICATE KEY`/`MERGE`
  **реализован** (см. [Upsert (key merge)](23-merge-statement.md#upsert-key-merge)), а полный `MERGE` с ветками
  `WHEN MATCHED`/`WHEN NOT MATCHED`/`WHEN NOT MATCHED BY SOURCE` доступен на SQL Server и
  PostgreSQL 15+ (см. [Полный `MERGE`](23-merge-statement.md#полный-merge)). `INSERT ... SELECT` **реализован**
  (см. «Батч из запроса» выше). (Строка «только дефолты» **поддерживается**: см. [Запись значений](#запись-значений).)
* **Мутации не готовятся и не кладутся в кэш планов.** Оптимизация в nextorm нацелена только на
  read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); мутация всегда рендерит и выполняет
  одну команду за вызов.
* **Число затронутых строк.** `Insert()`/`InsertAsync()` возвращают число строк, которое сообщает
  провайдер. ClickHouse для `INSERT ... VALUES` его не сообщает и возвращает `0`, хотя строка
  записана — не используйте счётчик для подтверждения вставки в ClickHouse.
* **Дробление большого батча.** `InsertInto<T>()` пишет одно утверждение; для всего набора используйте
  [Массовая вставка](24-bulk-insert.md), включая необязательное дробление
  (`MaxBatchSize`/`MaxParameters`/`MaxSqlLength`) и нативные bulk-пути.
* In-memory-провайдер только для чтения: `INSERT`/`UPDATE`/`DELETE` и полный `MERGE` бросают `NotSupportedException`; только key-upsert merge применяется к зарегистрированной последовательности в контексте.

## См. также

- [Слияние данных (MERGE / upsert)](23-merge-statement.md)
- [Массовая вставка (bulk)](24-bulk-insert.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
