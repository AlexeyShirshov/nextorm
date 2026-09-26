# Провайдер-специфика

## JSON-вывод (SQL Server)

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) — **терминальный оператор**: он выполняет запрос и возвращает
весь набор результатов одним JSON-документом ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)). Как терминал он не
добавляет неявный `TOP 1`, поэтому документ покрывает все строки; тип элемента запроса не важен,
так как база возвращает одну колонку-документ:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) строит документ по псевдонимам проекции, [`Auto`](xref:NextORM.Core.ForJsonMode.Auto) — по структуре
таблицы. `ForJson` возвращает `null`, если запрос не вернул строк (SQL Server отдаёт SQL NULL для
пустого результата `FOR JSON`). Предложение ставится после `ORDER BY` и перед завершающим
`OPTION (...)`; остальные провайдеры выбрасывают `NotSupportedException`. Используйте
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)), чтобы только *присоединить* предложение и сохранить команду
композируемой (для дальнейших хинтов или просмотра SQL).

## XML-вывод (SQL Server)

[`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[])) — XML-аналог и терминал ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)); поддерживаются
`RAW`, `AUTO`, `EXPLICIT` и `PATH`, с необязательным именем элемента строки, обёрткой `ROOT('...')` и
флагом `ELEMENTS`:

```csharp
string? xml = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id })
    .ForXml(ForXmlMode.Raw, elementName: "row", root: "items", elements: true);
```

```sql
select id from complex_entity for xml raw('row'), root('items'), elements
```

Как и `ForJson`, `ForXml` возвращает `null` для пустого набора, а
[`WithForXml`](xref:NextORM.Core.QueryCommand`1.WithForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean)) присоединяет предложение без выполнения. `FOR JSON` и `FOR XML` взаимно исключают
друг друга; их сочетание выбрасывает `NotSupportedException`.

## Блокировка строк (`FOR UPDATE` / `FOR SHARE`)

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) и
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) блокируют выбранные строки до конца
окружающей транзакции. PostgreSQL, MySQL и MariaDB генерируют завершающее предложение, которое
ставится последним — после `WHERE`, `ORDER BY` и запроса страницы; SQL Server вместо этого
привязывает табличный хинт `WITH (updlock)`/`WITH (holdlock)` к основной таблице:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id > 5)
    .ForUpdate()
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from simple_entity where (id > 5) for update

-- SQL Server
select id from simple_entity with (updlock) where (id > 5)
```

`ForUpdate()` блокирует строки монопольно; [`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare)
берёт разделяемую блокировку — PostgreSQL генерирует `for share`, MySQL/MariaDB генерируют
`lock in share mode`, а SQL Server — `holdlock` (разделяемая) против `updlock` для `ForUpdate`.
Предложение реализовано в PostgreSQL, MySQL, MariaDB и SQL Server
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock); в SQL Server — через
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints));
все остальные провайдеры выбрасывают `NotSupportedException` при построении SQL.

Режим ожидания задаётся [`LockWaitMode`](xref:NextORM.Core.LockWaitMode): если строку уже удерживает
другая транзакция, [`NoWait`](xref:NextORM.Core.LockWaitMode.NoWait) падает немедленно вместо
ожидания, а [`SkipLocked`](xref:NextORM.Core.LockWaitMode.SkipLocked) исключает занятые строки из
результата — стандартный приём для очередей и пулов воркеров:

```csharp
var claimed = await dataContext.From<Job>()
    .Where(x => x.State == "pending")
    .ForUpdate(LockWaitMode.SkipLocked)
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from job where (state = 'pending') for update skip locked

-- SQL Server (READPAST приближает SKIP LOCKED)
select id from job with (updlock, readpast) where (state = 'pending')
```

PostgreSQL и MySQL дописывают `nowait`/`skip locked` в конец
(`FOR UPDATE`/`FOR SHARE [NOWAIT | SKIP LOCKED]`); разделяемая блокировка с режимом переключает MySQL
с `lock in share mode` на `for share`, потому что `LOCK IN SHARE MODE` не принимает lock-option.
MariaDB дописывает режим и к `for update`, и к `lock in share mode` (`NOWAIT` с 10.3+, `SKIP LOCKED`
с 10.6+). SQL Server добавляет `nowait` или `readpast` в тот же табличный хинт
(`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` пропускает любую заблокированную
строку, а не только строку, удержанную другим писателем, поэтому он приближает, а не в точности
повторяет `SKIP LOCKED`. По умолчанию [`Wait`](xref:NextORM.Core.LockWaitMode.Wait) сохраняет
блокирующее поведение.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Псевдонимы колонок заключаются в одинарные кавычки (`as 'Calc'`); производные таблицы не требуют псевдонима. |
| SQL Server | Псевдонимы колонок заключаются в квадратные скобки (`as [Calc]`); каждая производная таблица должна иметь псевдоним (`as [t1]`). Конкатенация строк использует `+`. |
| PostgreSQL | Псевдонимы колонок заключаются в двойные кавычки (`as "Calc"`); производные таблицы должны иметь псевдоним (`as "t1"`). |
| MySQL | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| MariaDB | То же, что MySQL: обратные кавычки для псевдонимов, обязательный псевдоним производной таблицы и конкатенация через `concat(a, b)`. |
| ClickHouse | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| In-memory | SQL не генерируется; делегаты проекции компилируются и выполняются над объектами в памяти. |

