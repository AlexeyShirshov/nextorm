# Gap-анализ: открытый backlog linq2db против nextorm

> Источник: живой срез GitHub `linq2db/linq2db` на **2026-09-25** — 382 открытых issue, 8 активных
> `epic:`-меток и milestones `7.0.0`, `6.6.0`, `6.x`, `Backlog`, `In-progress`, `6.5.1`.
> Против среза 2026-09-23 прибавился один issue — `#5970` (Transform-mode optimization folds `IS NULL`,
> milestone `6.5.1`, баг конкретного провайдера, **N/A**); остальные счётчики совпадают.
> Снимок воспроизводится командой
> `gh issue list -R linq2db/linq2db --state open --limit 1000 --json number,title,labels,milestone`.
> База ссылок для `linq2db#N` — <https://github.com/linq2db/linq2db/issues/N>.
>
> Цель: понять, что linq2db **планирует добавить**, и какие из этих вещей отсутствуют в nextorm.
> Это дополняет [`linq2db-comparison.md`](linq2db-comparison.md) (сравнение *текущего*
> паритета) и [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) (пробелы по
> конструкциям SQL). Здесь — **вектор развития linq2db**, а не паритет по готовому функционалу.

## Как читать

| Статус | Значение |
|---|---|
| **<span style="color:green">Done</span>** | nextorm уже это умеет (подтверждено кодом/доками/тестами) |
| **Planned** | есть `todo_*.md`; в этом документе не является новым пробелом |
| **Gap** | реальный недостающий функционал в scope nextorm; кандидат на `todo_*.md` |
| **Out-of-scope** | осознанная граница nextorm (не «пробел»); см. [Limitations](../../advanced/limitations.md) |
| **By design** | осознанное решение, не gap: поведение достижимо явными примитивами и документировано |
| **N/A** | внутренняя механика linq2db, баг конкретного провайдера или экосистема вне scope |

Обновление статусов nextorm — на `2026-09-25`: `INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, returning/
output, композируемый DML `RETURNING` (data-modifying CTE), CTAS, массовая вставка
(`BulkInsertOptions`/`BulkInsertOptionsBuilder`) и транзакции (`ITransactionManager`) считаются сделанными по
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4/§6; строки в
`comparison/`-документах приведены в соответствие (см. §7). Также сняты как реализованные: `[Duration]`/
`TimeSpan`-колонки (~~G9~~), PG JSONPath (~~G15~~), логирование параметров через интерцепторы
(~~G10~~-логирование), продвижение sub-day-операнда (`linq2db#5965`, `ISqlDialect.PromoteDateOperand`) и
трансляция CLR `Regex` на SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`, ~~G7~~-хвост).

Кроме того, закрыт майлстоун **1.0-b.1** (релизные задачи nextorm #15/#31/#63/#64/#66): сняты как
реализованные value converters (~~G1~~, фазы 1–2), JSON-колонка ↔ CLR-объект (~~G2~~), `OUTPUT INTO` +
upsert-with-output (~~G4~~, кроме multi-result), PostgreSQL range/`Overlaps` (~~G11~~, см. §3),
range поверх пары скалярных колонок (`[RangeColumns]`, `SupportsRangeColumns` — провайдеры без нативного
range-типа) и динамическая схема табличных источников (ClickHouse `values()`, PostgreSQL
`jsonb_to_record(set)` — §5).
Открытыми по-прежнему остаются: multi-result-set
(~~G4~~-хвост), SQLite JSON TVF (G14), ограничение кэша (G10), version-gates (G13), TVP/хранимые
процедуры (G8).

## 1. Epic-уровень: куда линq2db вкладывается

| Epic linq2db | Откр. | Что это | Статус nextorm | Решение |
|---|---|---|---|---|
| `epic: DDL` | 18 | `CREATE`/`ALTER`/`DROP TABLE`, constraints, индексы, sequences, enums, `Create/Drop Database` | CTAS есть (`ToTempTable`/`ToTable`), управления схемой нет | **Gap (решение нужно)**: либо осознанно out-of-scope, либо новый workstream «DDL» |
| `epic: code-generator` | 21 | CLI/T4-скаффолдинг маппингов из живой БД | none (маппинги только в коде) | **Out-of-scope** (заявленная граница) |
| `epic: eager-load` | 12 | `[Association]`, `LoadWith`, `Include`, ordering/strategy | none (только явные join'ы) | **Out-of-scope** (нет метаданных связей; решение зафиксировано — gap-analysis §5 п.49, [`todo_eager_loading.md`](../roadmap/todo_eager_loading.md)) |
| `epic: insert` | 16 | полнота INSERT/UPSERT, bulk, output | `INSERT VALUES/SELECT`, key-upsert, full MERGE, bulk — <span style="color:green">Done</span> | смешанно: см. §4 |
| `epic: json_sql` | 5 | JSON-типы, авто-сериализация объектов, `jsonpath`, SQLite TVF | PG native JSON + text-JSON + ClickHouse + PG `jsonpath` — <span style="color:green">Done</span>; объект↔JSON (фаза 1: `[JsonColumn]`) — <span style="color:green">Done</span>; SQLite TVF — нет | **Gap** (G14) |
| `epic: merge` | 5 | MERGE: immutable-модели, частичные setters, TPH/EF | full MERGE (SQL Server, PG15+) — <span style="color:green">Done</span>; inheritance/EF — out-of-scope | частично **Gap** (G-merge) |
| `epic: output` | 6 | `OUTPUT`/`OUTPUT INTO`, несколько result-set'ов, INSERT…WithOutput в CTE | returning/output одного стейтмента — <span style="color:green">Done</span>; композируемый `INSERT ... RETURNING` как data-modifying CTE (PG) — <span style="color:green">Done</span>; `OUTPUT INTO` (SQL Server) и output у key-upsert — <span style="color:green">Done</span> | много-result-set — остаётся хвостом ~~G4~~/`todo_output_into.md`; ~~G3~~ закрыт |
| `epic: new-provider` | 4 | Oracle/Redshift/Sybase/SAP | provider breadth — граница | **Out-of-scope** |

Плюс сквозные `area:`-кластеры (не эпики): `area: types` (унификация типов, TimeSpan/interval),
`area: performance` (кэш запросов, оптимизатор), `area: mapping` (fluent, converters), `area: hints`,
`area: extensions`, `area: infrastructure`.

## 2. Milestone-уровень: ближайший вектор linq2db

### `7.0.0` — «Next major release» (4)
Крупные архитектурные изменения ядра, для nextorm — **N/A** (информативно):
`linq2db#5675` schema-aware metadata readers, `#5718` schema-bound `EntityDescriptorsCache`,
`#5758` type mapping на SQL-выражениях вместо `ColumnDescriptor`, `#5812` `IInfrastructure<IServiceProvider>`
на `IDataContext`. Пересечение с nextorm: наш `DataContextCache`/`IEntityMetadata` решают похожие задачи
(см. `todo_interface_poco.md`, `todo_public_api_freeze.md`) — полезно как ориентир, не как gap.

### `6.6.0` (44) и `6.x` (20) — в основном корректность/провайдеры
Из списка вычленяются feature-пункты, релевантные nextorm (остальное — багфиксы конкретных провайдеров
и инфраструктура):

| linq2db | Тема | Статус nextorm |
|---|---|---|
| `#5759` | `TimeSpan`-члены и сравнения на native interval-колонках (`[Duration]`) | **<span style="color:green">Реализовано</span> (1.0.6-alpha, ~~G9~~)**: `DurationUnit`/`DurationAttribute`/fluent `Duration(...)`, native `interval`/`TIME` → [Duration-колонки](../../guide/26-duration-columns.md), [Duration columns](../../guide/26-duration-columns.md) |
| `#5933` | диалект MariaDB 13 | **Gap** (G13) |
| `#5948`, `#5952` | PG 9.2/9.3: `FILTER`-агрегаты синтаксически недоступны | **Gap** (G13, version-gate) |
| `#5961`, `#5914` | ClickHouse date/`DateTimeOffset` типы в SQL противоречат декларации | **Проверено, не gap.** `#5961`: у nextorm нет per-node `DbDataType`, а части даты уже приведены к объявленному CLR-типу (`toInt32(toISOWeek(toDateTime64(…)))`, `toInt32(toYear(…))`, `toFloat64(toUnixTimestamp(…))`); `to_unix_timestamp` объявлен `long` и обёрнут `toInt64` — «type lie» не воспроизводится. `#5914`: `DateTimeOffset` получил read-ветку (`GetFieldValue<DateTimeOffset>`; см. [Duration columns](../../guide/26-duration-columns.md)), но расхождение Date/DateTime64 не воспроизводится — **N/A**. Однотипный хвост — ширина `date_diff`, см. G20; общий план — [Duration columns](../../guide/26-duration-columns.md) |
| `#5921` | `string.Format`/интерполяция: format-спецификаторы молча теряются | **Реализовано** (G12) → [Ordinal-сравнение и коллация](../../ru/scalar-functions/01-string-functions.md#ordinal-сравнение-и-коллация) |
| `#5927` | `string.CompareOrdinal`/ordinal `Compare` маппятся в culture-sensitive | **Реализовано** (G12): ordinal `Compare`/`CompareOrdinal`/`Equals`/`Contains` переводятся в бинарную коллацию, неподдержанные формы бросают `NotSupportedException` → [Ordinal-сравнение и коллация](../../ru/scalar-functions/01-string-functions.md#ordinal-сравнение-и-коллация) |
| `#5965` | sub-day date-функции над date-only операндами | **<span style="color:green">Реализовано</span> (1.0.6-alpha).** Ранее nextorm не продвигал date-only операнд: `date_add`/`DateTime.Add*` и `.Hour/.Minute/.Second` рендерились на колонке (SQL Server `date`-колонка → 9810, ClickHouse `Date` теряет sub-day). Теперь хук `ISqlDialect.PromoteDateOperand(field, value)` продвигает операнд перед sub-day функцией: SQL Server `cast(value as datetime2)`, ClickHouse `toDateTime` (hour..second) и `toDateTime64(value, 3 или 6)` (milliseconds/microseconds), PG/MySQL/SQLite — без изменений. Полноценные precision-метаданные колонки (§8.5) остаются открытыми. См. [Duration columns](../../guide/26-duration-columns.md)§9.4 |
| `#5837`, `#5838`, `#5852` | inheritance/TPH write, shadowing-член | **Out-of-scope** (нет TPH) |
| `#5904`, `#5937`, `#5941`, `#5940`, `#5865` | eager-load ordering/strategy | **Out-of-scope** |
| `#5717` | DML `RETURNING`/`OUTPUT` как **композируемый** `IQueryable`-источник | **Частично <span style="color:green">Done</span>** (~~G3~~): PG `INSERT ... RETURNING` как data-modifying CTE |
| `#4562` | PostgreSQL `Overlaps` (range `&&`) | **<span style="color:green">Реализовано</span>** (G11, 1.0-b.1); плюс range поверх пары скалярных колонок (`[RangeColumns]`) на провайдерах без нативного range → [PostgreSQL-specific SQL](../../guide/provider-specific/postgresql.md#range-types), [Range columns](../../guide/31-range-columns.md) |
| `#4543` | декларативный `QueryFilter`-атрибут | **Planned** → [`todo_query_filters.md`](../roadmap/todo_query_filters.md) |
| `#5706`, `#1879`, `#3740`, `#5425` | Oracle-специфика | **Out-of-scope** (нет провайдера) |
| `#3023`, `#5895`–`#5897` | Sybase/DB2-специфика | **Out-of-scope** |
| `#4745`, `#5081`, `#5911`, `#5903`, `#5861`, `#5862`, `#5730` | внутренности/SQL-gen/упаковка | **N/A** |

### `Backlog` (44, «Available tasks») — фичи, не привязанные к релизу

| linq2db | Тема | Статус nextorm |
|---|---|---|
| `#698` | `Regex` внутри запроса (трансляция `Regex.IsMatch`/…) | **<span style="color:green">реализовано</span>** (~~G7~~): [Регулярные выражения](../../ru/scalar-functions/01-string-functions.md#регулярные-выражения) |
| `#1645` | table-valued **parameters** для хранимых процедур (TVP) | **Gap** (G8) → [`todo_tvp.md`](../roadmap/todo_tvp.md); смежно [`todo_stored_procedures.md`](../roadmap/todo_stored_procedures.md) |
| `#1994` | open-generic `TypeConverter` | **Done** (G1, фазы 1–2: `IPropertyValueConverter`/`ValueConverter<,>`/`[ValueConverter]`/`.HasConversion`, константы в предикатах/`IN` и скалярных проекциях) → [Value converters](../../guide/30-value-converters.md) |
| `#3009` | ограничение размера кэша запросов | **Gap** (G10) |
| `#4039` | логирование SQL-параметров | **<span style="color:green">Реализовано</span>** (интерцепторы, 1.0.6-alpha): `IQueryInterceptor` (`CommandInitialized`/`CommandExecuting`) отдаёт привязанную команду, интерцептор читает `command.Parameters` → [гайд 27](../../guide/27-interceptors.md) |
| `#4405` | concurrency check с явными исключениями | **By design** (~~G6~~): достижимо через `Where` + число затронутых строк; гайд [Оптимистичная конкурентность и отслеживание изменений](../../guide/29-optimistic-concurrency.md) |
| `#4199` | «Property X is not defined for interface type Y» | **Planned** (`todo_interface_poco.md`) |
| `#5822` | рекурсивный CTE с `UNION` и вычисляемой проекцией | **<span style="color:green">Не подтвердилось — уже было реализовано</span>** (~~G16~~): union рендерится inline, self-ref не оборачивается |
| `#5879` | `IndexExpression` (`x[i]`) в дереве запроса | **<span style="color:green">Реализовано</span>** (~~G17~~): captured `dict`/`list`/`arr`, включая интерфейсные типы → `CASE`/параметр; неподдерживаемый индексер → `NotSupportedException` → [Filtering](../../guide/02-filtering-where.md#captured-collection-lookup-dictcolumn) |
| `#3015` | `UPDATE` через CTE (несколько провайдеров) | **<span style="color:green">Done</span>** (~~G18~~): `WITH ... UPDATE`/`DELETE` эмитится, CTE хойстится перед мутацией |
| `#4139` | composite-объекты для associations | **Out-of-scope** |
| `#1181`, `#1651`, `#3914`, `#4314`, `#5527`, `#5853`, `#5903` | типы/mapping/инфраструктура | **N/A** |
| остальные (`#86`, `#1880`, `#4436`, …) | багфиксы провайдеров | **N/A** |

### `6.5.1` (8) и `In-progress` (5) — корректность
Почти всё — регрессии `PreferClientCalculation`/eager-load/провайдеров (**N/A**), включая новый `#5970`
(Transform-mode optimization сворачивает `IS NULL` на колонке производной таблицы), кроме `#698`
(~~G7~~ реализовано) и `#4306`/`#2950` (`TimeSpan`-типы на SQL Server/PostgreSQL → ~~G9~~ реализовано).

### Что linq2db уже **выпустил** в 6.5.0 и чего у nextorm нет
Не backlog, но свежий вектор (release notes wiki):
`UpdateOptimisticWithRefresh` (оптимистичный update с write-back токена) → ~~G6~~ закрыт как **By design**
(нет change tracking → write-back не операция фреймворка; паттерн — [гайд](../../guide/29-optimistic-concurrency.md));
`[Duration]`/`TimeSpan`-колонки → ~~G9~~ <span style="color:green">реализовано</span>; `BulkCopyOptions.MaxSqlLengthForBatch` → <span style="color:green">реализовано</span>
(`MaxBatchSize`/`MaxParameters`/`MaxSqlLength` у `BulkInsertInto`); F# `option`/single-case discriminated
unions → **Out-of-scope** (F#);
LINQPad/gRPC-remote-context → **Out-of-scope**.

## 3. Приоритезированные in-scope пробелы

### P0 — усиливают уже выбранное направление (writing/JSON/bulk)

**~~G1~~. Value converters / кастомный маппинг типов — <span style="color:green">Done</span> (1.0-b.1, фазы 1–2).**
`linq2db#1994` (open generic) и общая тема `area: mapping`/`area: types`.
**<span style="color:green">Реализовано</span>**: `IPropertyValueConverter` / `ValueConverter<TModel,TProvider>`
(строго типизированная база с кэшированным инвокером, без боксинга value-типов на строку),
атрибут `[ValueConverter(typeof(...))]`, fluent `.HasConversion(...)` на `EntityPropertyBuilder<T>`,
член `IPropertyMetadata.Converter` (default member) и единый write/read-сид
(`RowMapperFactory`, `SqlMutationBuilder`); identity конвертера входит в ключ плана/кэша.
Без этого JSON-колонки, enum-as-string, `DateTimeOffset`-в-строках и кастомные типы требовали ручных
проекций. См. [Value converters и JSON-колонки](../../guide/30-value-converters.md); фазы 1–2
поставлены — константы-конвертеры в предикатах/`IN` и скалярных/анонимных проекциях.

**~~G2~~. JSON-колонка ↔ CLR-объект (авто-сериализация) — <span style="color:green">Done</span> (1.0-b.1).**
`linq2db#1661`. **<span style="color:green">Реализовано</span>**: `[JsonColumn]` +
`JsonColumnStorage`/`JsonColumnOptions` + `JsonColumnConverter<,>` поверх `System.Text.Json`; нативная
`jsonb`-колонка (PostgreSQL) и текст (SQL Server/MySQL/MariaDB/ClickHouse/SQLite); read/write/UPDATE/
MERGE/`Returning`. См. [Value converters и JSON-колонки](../../guide/30-value-converters.md);
фазы 1–2 поставлены (round-trip, `Returning`, проекции и сравнение с константой). Остаток по
[`todo_json_streaming.md`](../roadmap/todo_json_streaming.md) (общий STJ-слой для стриминга); AOT-фаза
(`JsonTypeInfo<T>`) — вне области.

**~~G3~~. DML `RETURNING`/`OUTPUT` как композируемый источник — <span style="color:green">Done</span> (PostgreSQL).** `linq2db#5717`.
<span style="color:green">Реализовано</span>: `ctx.With("ins", ctx.InsertInto<T>().Values(v).Returning(x => new { x.Id }))` открывает
`MutationCteQuery<TResult>`; `INSERT ... RETURNING` рендерится как data-modifying CTE (гейт
`ISqlDialect.SupportsDataModifyingCtes`), а его строки читаются через `.From("ins")` и дальше
фильтруются/джойнятся/проецируются как обычный источник (вплоть до питания внешнего `INSERT ... SELECT`);
`MutationCteQuery.With`/`FromTable` позволяют объявить рядом обычные read-CTE. Провайдеры без
data-modifying CTE кидают `NotSupportedException`. Тесты: SQL-gen
(`tests/nextorm.postgres.tests/InsertSqlGenerationTests.cs`) и интеграция
(`PostgresSpecificTests.DataModifyingCte*`). См. [CTE](../../guide/09-cte.md#data-modifying-cte-postgresql)
и [INSERT](../../guide/19-insert-statement.md#data-modifying-cte-postgresql). Непокрытый хвост `#5717`
(composable `OUTPUT`/`UPDATE`/`DELETE`/`MERGE`-output, SQL Server composable-DML) остаётся открытым; в
~~G4~~ он не входит — G4 закрыл `OUTPUT INTO` и output у key-upsert, но не composable-DML.

**~~G4~~. `OUTPUT INTO` + несколько result-set'ов + upsert-with-output — <span style="color:green">Done</span> (1.0-b.1).**
`linq2db#3832` (`...WithOutputIntoOutput`), `#2982` (multi-result-set),
`#3124`/`#4824` (InsertOrUpdate/InsertOrActionWithOutput). **<span style="color:green">Реализовано</span>**:
SQL Server `OUTPUT ... INTO <table>` (в т.ч. `INTO` + клиентский результат через `OutputIntoThenOutput`)
и `Returning()` у key-upsert (PostgreSQL/SQLite `ON CONFLICT ... RETURNING`, SQL Server `MERGE ... OUTPUT`).
Журнал — [`todo_output_into.md`](../roadmap/todo_output_into.md). **Остаётся** фаза несколько
result-set'ов: переиспользуется существующая навигация `BatchRunner`, отдельный API не заводился.

**~~G5~~. Bulk-insert: возврат идентификаторов и конфликтная политика.** `linq2db#2960` (Returning IDs),
`#5124` (Bulk Insert Ignore), `#3795` (identity insert). **<span style="color:green">Реализовано</span>**: `BulkInsertInto<T>()` —
нативный `COPY`/`SqlBulkCopy` для PostgreSQL/SQL Server, портируемый `INSERT ... VALUES` с чанкингом для
остальных; возврат ключей — batch `RETURNING`/`OUTPUT` (`ReturningKey`/`Returning`), пропуск конфликтов —
опция `BulkInsertOptions.IgnoreDuplicates` на диалектных хуках
`SupportsInsertIgnore`/`SupportsOnConflictDoNothing`, запись identity — `BulkInsertOptions.KeepIdentity`
(`OVERRIDING SYSTEM VALUE`/`SET IDENTITY_INSERT`) и чанкинг `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`
(опции передаются в `BulkInsertInto<T>(options)` или через `BulkInsertOptionsBuilder`).
См. [Массовая вставка](../../guide/24-bulk-insert.md).

**~~G6~~. Оптимистичная конкурентность — <span style="color:green">By design</span>.** `linq2db#4405` и
shipped `UpdateOptimisticWithRefresh` (6.5.0). При отсутствии change tracking/identity map фреймворку
нечем «владеть»: write-back токена — не операция фреймворка, а присваивание в объекте вызывающего кода,
поэтому 1:1-перенос `UpdateOptimisticWithRefresh` в модель без трекинга смысла не имеет. Достаточно явных
примитивов, которые уже есть: `Update<T>().Set(...).Where(key && token).Update()` возвращает число
затронутых строк (`0` = конфликт), `Returning`/`OUTPUT` отдаёт новый токен, а `Merge` с условной веткой
`WhenMatched((t, s) => t.Version == s.Version)` даёт upsert-с-проверкой (SQL Server, PG15+).
`IfUnchanged`/`WithRefresh` были бы чистым сахаром над `Where`; вместо API — гайд
[Оптимистичная конкурентность и отслеживание изменений](../../guide/29-optimistic-concurrency.md).
`todo_optimistic_concurrency.md` не заводится.

### P1 — расширение паритета

**~~G7~~. `Regex` в запросе — <span style="color:green">Done</span>.** `linq2db#698` (`area: extensions`, `area: sql`). **<span style="color:green">Реализовано</span>**:
`Regex.IsMatch`/`Regex.Replace` с константным шаблоном транслируются на PostgreSQL (`~`/`~*`,
`regexp_replace` с `'g'`), MySQL (`REGEXP_LIKE`/`REGEXP_REPLACE` с match type), MariaDB (`REGEXP`/
`REGEXP_REPLACE` с `(?i)`/`(?-i)`), ClickHouse (`match`/`replaceRegexpAll` с `(?i)`), SQLite
(регистрируемые CLR-функции `regexp`/`regexp_replace`) и SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`;
match требует database compatibility level 170, на 2019/2022 вызов отклоняется движком).
См. [Регулярные выражения](../../ru/scalar-functions/01-string-functions.md#регулярные-выражения) и
[Limitations и out-of-scope](../../advanced/limitations.md).

**G8. Table-valued parameters (TVP).** `linq2db#1645`. nextorm умеет TVF как **источник**, но не
передачу таблицы **параметром** (SQL Server `SqlParameter` с structured type; PG — array/`unnest`).
Действие: [`todo_tvp.md`](../roadmap/todo_tvp.md).

**~~G9~~. `TimeSpan`/interval-колонки (`[Duration]`) — <span style="color:green">Done</span> (1.0.6-alpha).** `linq2db#5759`, `#4306`, `#2950`.
**Реализовано:** `DurationUnit`/`DurationAttribute`/fluent `Duration(...)`,
read/write/сравнения, native `interval` (PG) и `TIME` (MySQL/MariaDB), целочисленная форма для
SQL Server/SQLite/ClickHouse; публичная страница — [`docs/guide/26-duration-columns.md`](../../guide/26-duration-columns.md)
(+RU). Остаток (sub-day promotion §8.2, ширина `date_diff` §8.3) — **<span style="color:green">реализовано (1.0.6-alpha)</span>**, см.
[Duration columns](../../guide/26-duration-columns.md)

**G10. Ограничение кэша планов + логирование параметров — <span style="color:green">частично Done</span>.** `linq2db#3009` (cache size), `#4039`
(parameter logging). **<span style="color:green">Реализовано</span>** логирование параметров: фаза 1 интерцепторов (1.0.6-alpha)
отдаёт привязанную команду на `CommandInitialized`/`CommandExecuting`, интерцептор читает `command.Parameters`
([гайд 27](../../guide/27-interceptors.md)). Остаётся `#3009` — риск неограниченного роста process-wide
`DataContextCache` (`MapperCache` уже ограничен `MaxEntries = 4096`); действие: добавить LRU/размер в
кэш-инфраструктуру.

**~~G11~~. PostgreSQL range/`Overlaps` — <span style="color:green">Done</span> (1.0-b.1).**
`linq2db#4562`; shipped `Sql.Row.Overlaps` (6.5.0). Добавлены provider-agnostic `NextORM.Core.Range<T>`
и PG-only поверхность `SqlFunctions.Postgres` (`overlaps` → `&&`, `range_contains`/`range_contained_by`,
позиционные/смежные/union/intersection/difference операторы, `lower`/`upper`/`isempty`, конструкторы),
гейт `ISqlDialect.SupportsRanges`, round-trip `Range<T>` ↔ `NpgsqlRange<T>`. Смежно закрыт пробел для
провайдеров без нативного range: `[RangeColumns]` (гейт `ISqlDialect.SupportsRangeColumns`) хранит
`Range<T>` как **пару скалярных колонок** и транслирует предикаты/инспекцию (`overlaps`,
`range_contains`/`range_contained_by`, позиционные/смежные, `lower`/`upper`/`isempty`) поверх пары; такого
маппинга у linq2db нет. См.
[PostgreSQL-specific SQL](../../guide/provider-specific/postgresql.md#range-types) и
[Range columns](../../guide/31-range-columns.md).

**~~G12~~. C# string-семантика — <span style="color:green">Done</span> (1.0.6-alpha).** `linq2db#5921` (format-спецификаторы в `string.Format`/`$"…"`),
`#5927` (ordinal `Compare`/`CompareOrdinal` сворачиваются в culture-sensitive `CompareTo`). В nextorm это
**реализовано** (G12): `string.Compare`/`CompareOrdinal`/`Equals`/`Contains`/`StartsWith`/`EndsWith`/
`IndexOf`/`LastIndexOf` с константным `StringComparison` переводятся через бинарную коллацию
(`Ordinal`) или её свёртку (`OrdinalIgnoreCase`), а `string.Format`/интерполяция/`ToString(format)` — в
родную функцию провайдера для culture-invariant подмножества спецификаторов. Ни одна неподдержанная
форма не даёт молча неверный SQL: она бросает `NotSupportedException`. Колонковая коллация (nextorm
issue `#28` «column collation») на том же примитиве `MakeCollate` тоже **сделана**: коллация
объявляется на свойстве сущности (`CollationAttribute`/`EntityPropertyBuilder<T>.Collation`) и
применяется в collation-чувствительных операциях запроса (ClickHouse без `COLLATE` отклоняет).
Связанные задачи G12 — верность трансляции C#-семантики, `#28` — объявление/маппинг: обе закрыты.
См. [Ordinal-сравнение и коллация](../../ru/scalar-functions/01-string-functions.md#ordinal-сравнение-и-коллация) и
[Ограничения](../../advanced/limitations.md).

**G13. Версионные диалекты и version-gates.** `linq2db#5933` (MariaDB 13), `#5948`/`#5952`
(PG 9.2/9.3 не поддерживают `FILTER` в агрегатах). nextorm генерирует `FILTER`-агрегаты на PG и
имеет единый MariaDB-диалект без версии. Действие: version-flag в `ISqlDialect`/`MariaDbDialect`,
аналогично существующим `Supports*`.

### P2 — watchlist / проверить

| # | linq2db | Что проверить в nextorm |
|---|---|---|
| G14 | `#3408` SQLite `json_each`/`json_tree` | нет built-in TVF; выразимо `[SqlTableFunction]`-обёрткой — решить, нужен ли built-in |
| ~~G15~~ | `#3869` PG `jsonb` jsonpath | **<span style="color:green">Done</span>** — скаляры `jsonb_path_exists`/`jsonb_path_match`/`jsonb_path_query_first`/`jsonb_path_query_array`, `jsonpath(cast)` и TVF `[SqlTableFunction("jsonb_path_query")]`; покрыто [гайдом 18](../../guide/18-json.md) и PG-тестами (SQL-gen/интеграция) |
| ~~G16~~ | `#5822` recursive CTE `UNION` + вычисляемая проекция | **<span style="color:green">Не подтвердилось — уже было реализовано</span>** — union inline, self-ref не оборачивается; distinct-`Union` покрыт тестами (SQLite/PG/MySQL; SQL Server отвергает — требует `UNION ALL`) |
| ~~G17~~ | `#5879` `IndexExpression` в дереве | **<span style="color:green">Done</span>** — captured `dict`/`list`/`arr`, включая интерфейсные типы (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`, `IList<T>`, `IDictionary<,>`): ключ-константа → параметр, ключ-колонка → `CASE`; неподдерживаемый индексер (серверный массив/JSON, кастомный) → `NotSupportedException` (см. ниже) |
| ~~G18~~ | `#3015` `UPDATE` через CTE | **<span style="color:green">Done</span>** — `WITH ... UPDATE`/`DELETE` хойстится перед мутацией (см. ниже) |
| G19 | `#5675`/`#5718`/`#5758` | архитектурный ориентир для метаданных/кэша, не gap |

**~~G15~~. PostgreSQL `jsonb` jsonpath — <span style="color:green">Done</span> (проверено).** `linq2db#3869`.
Проверено кодом: nextorm имеет скалярную поверхность JSONPath — `jsonb_path_exists`/`jsonb_path_match`/
`jsonb_path_query_first`/`jsonb_path_query_array` и `jsonpath(cast)` (функциональная форма, эквивалентная
операторам `@?`/`@@`) — плюс TVF-источник `[SqlTableFunction("jsonb_path_query")]`
(`SqlFunctions.Postgres.cs`). Разрыв, заявленный в `#3869`, отсутствует; покрытие —
[гайд 18](../../guide/18-json.md), SQL-gen `tests/nextorm.postgres.tests/SqlGenerationTests.cs`/
`PostgresDialectTests.cs` и интеграция `PostgresSpecificTests`.

**~~G16~~. Рекурсивный CTE с `UNION` (distinct) и вычисляемой проекцией.** `linq2db#5822`. Проверено
генерацией на SQLite: `WithRecursive("nums", anchor.Union(step))` с внешней проекцией `n * 2` даёт
`with recursive nums as (select … union select … from nums …) select (n * 2) as 'n' from nums` — union
рендерится **inline** (`SqlBuilder.MakeSelect`, ветка `UnionQuery`, `SqlBuilder.cs:280`), derived-table
обёртки, как в linq2db, нет, self-reference остаётся на разрешённом месте. **<span style="color:green">Не подтвердилось — уже было реализовано</span>**; архитектурное
отличие: у nextorm нет оптимизатора, складывающего проекцию в ноги set-операции. Покрытие добавлено:
SQL-gen — `tests/nextorm.sqlite.tests/SqlGenerationTests.cs` и `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
(`Cte_Recursive_WithDistinctUnion_ShouldEmitUnionNotUnionAll`), интеграция per-provider —
`SqliteSpecificTests`/`PostgresSpecificTests`/`MySqlSpecificTests`
(`Cte_Recursive_WithDistinctUnion_ShouldProduceNumberSeries`), т.к. **SQL Server отвергает**
рекурсивный CTE с top-level distinct `UNION` («Recursive common table expression 'nums' does not
contain a top-level UNION ALL operator» — проверено интеграцией), поэтому тест не в общем
`CommonTestSuite`. На T-SQL distinct-`Union` в `WithRecursive` даёт невалидный SQL — кандидат на gate.

**~~G17~~. `IndexExpression`/`x[i]` в дереве — <span style="color:green">Done</span> (1.0-b.1).** `linq2db#5879`.
**<span style="color:green">Реализовано</span>** (движковый `DictionaryLookup`, поставлен в 1.0-b.1):
индексация замкнутой коллекции — `dict[column]`, `list[column]`, `arr[i]`, включая коллекцию,
объявленную **интерфейсом** (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`, `IList<T>`,
`IDictionary<,>`, immutable) — транслируется в портируемый `CASE WHEN key = @k THEN @v … END`;
ключ-константа сворачивается в скалярный параметр; число ветвей входит в ключ плана; in-memory
вычисляет индексер нативно. Раньше интерфейсно-типизированная коллекция при ключе-колонке **молча**
биндила весь объект (`somestring = $values0`) — теперь это исправлено. Неподдерживаемый индексер
(поэлементный доступ к серверному массиву/JSON, кастомный индексер) явно бросает
`NotSupportedException` вместо неверного SQL (минимальное требование пункта). См.
[Filtering](../../guide/02-filtering-where.md#captured-collection-lookup-dictcolumn) и
[Limitations](../../advanced/limitations.md).

**~~G18~~. `WITH ... UPDATE` / `WITH ... DELETE` — <span style="color:green">Done</span>.** `linq2db#3015`. Раньше join цели к CTE-источнику
`ctx.With("c", q).From("c")` давал `update simple_entity as 't1' set id = $p0 from c as 't2' where t1.id = t2.id`
— **без** `with c as (…)`, т.е. невалидный SQL: `JoinedEntityBuilder` брал `Ctes` только с левой стороны,
а мутационный рендер не хойстил `WITH` (в отличие от `RenderSource`). Исправлено: `internal static CteMerge`
сливает декларации обеих сторон join (левые раньше правых; переданные тем же экземпляром дедуплицируются,
два разных объявления с одним именем отклоняются `InvalidOperationException`), `JoinSourceResolver`
резолвит имя CTE/таблицы в `FromExpression` на любой позиции join (раньше 2-й/последующий join с
имя-источником падал `NullReferenceException`), а `QueryPlanner.RenderUpdateJoin`/`RenderDeleteJoin`
хойстят `WITH` перед мутацией, деля параметр-провайдер с телом (и дописывая `OPTION (MAXRECURSION n)`
для рекурсивного CTE на SQL Server). Тесты: SQL-gen (SQLite/PG/SQL Server/MySQL/MariaDB — 2-й join,
рекурсивный CTE PG/SQL Server), негатив на коллизию имён (core), интеграция в `CommonTestSuite`
(PG/SQL Server/MySQL/SQLite для UPDATE; PG/SQL Server/MySQL для DELETE — SQLite не имеет multi-table
DELETE). См. [CTE](../../guide/09-cte.md), [UPDATE](../../guide/21-update-statement.md),
[DELETE](../../guide/20-delete-statement.md).

**~~G20~~. Ширина результата `date_diff` — <span style="color:green">Done</span> аддитивно (найдено при проверке `#5961`).** `CommonFunctions.date_diff`
(`src/nextorm.core/Query/SqlFunctions.cs:525`) объявлен `int?`, но ClickHouse рендерит
`dateDiff('unit', …)` → **Int64**, а `SelectExpression.GetDataRecordMethod()` читает его как `GetInt32`.
Для `seconds`/`milliseconds`/`microseconds` значение легко превышает Int32 (~24,8 дня для мс, ~35,8 мин
для мкс). SQL Server/PG base отдают `int` (PG кастит `… as integer` явно) — там расхождения нет;
**✅ Реализовано аддитивно (24.09.2026).** `date_diff` остаётся `int?`; добавлен `date_diff_big → long?`
с хуком `ISqlDialect.MakeDateDiffBig` (SQL Server `datediff_big`, PostgreSQL `bigint`, остальные
делегируют `MakeDateDiff`). Добавлены ClickHouse SQL-gen `date_diff`/`date_diff_big` и common
integration-тест. Подробности — [Duration columns](../../guide/26-duration-columns.md)§9.4.

## 4. Осознанно вне scope (не считать пробелами)

Совпадает с [`linq2db-comparison.md`](linq2db-comparison.md) «Deliberate boundaries»:

- **DDL/управление схемой** (`epic: DDL`): `CREATE`/`ALTER`/`DROP TABLE`, constraints, индексы, sequences,
  enums, `Create/Drop Database`. nextorm мутирует схему только через CTAS. Это **единственный крупный
  непокрытый эпик**, где нужно явное решение: осознанно оставить или завести workstream «DDL» (в
  `sql-capabilities-gap-analysis.md` DDL сейчас в «Future workstreams (not scheduled)»).
- **Связи/eager-load** (`epic: eager-load`) и **inheritance/TPH**. Eager loading закрыт решением (не
  планируется до появления навигаций) — gap-analysis §5 п.49, [`todo_eager_loading.md`](../roadmap/todo_eager_loading.md).
- **Скаффолдинг/кодогенерация** (`epic: code-generator`).
- **Новые провайдеры** (`epic: new-provider`): Oracle, Firebird, DB2, SAP HANA, Informix, Sybase,
  Redshift, DuckDB, YDB, Access, SQL CE.
- **Remote context / gRPC**, **LINQPad-драйвер**, **F#-специфика**.
- **Change tracking / identity map**.
- **Query filters** — не out-of-scope, а `Planned` ([`todo_query_filters.md`](../roadmap/todo_query_filters.md)).
- **EF Core integration** — не out-of-scope, а `Planned` ([`todo_efcore_integration.md`](../roadmap/todo_efcore_integration.md));
  `linq2db#4044`, `#4611`, `#4666` закрываются ей.

### Пропущенная ось: shipped-поверхность `LinqExtensions`

Развёртка §1–§3 идёт по **открытым** issue/epic linq2db (`gh issue list`, срез 2026-09-25), поэтому
выпущенные ядровые операторы и расширения `LinqExtensions`, которым не соответствует ни открытый issue,
ни отдельная SQL-конструкция, в gap-анализ не попали. Их SQL-эквивалент у nextorm уже есть (или эти
возможности сознательно не переносятся), поэтому статус — **By design**; исключения отмечены **Gap**.

| Возможность linq2db (shipped) | Эквивалент в nextorm | Статус |
|---|---|---|
| `SelectMany` (flatten, `CROSS APPLY`) | [`CrossApply`](../../guide/03-joins.md) | **By design** — SQL-провайдеры бросают `NotSupportedException`, in-memory реализован ([Limitations](../../advanced/limitations.md)) |
| `GroupJoin` (grouped inner, `LEFT JOIN`) | [`LeftJoin`](../../guide/03-joins.md) + `GROUP BY`/агрегат | **By design** |
| `DefaultIfEmpty` (left-join-семантика `SelectMany`) | [`LeftJoin`](../../guide/03-joins.md) / `OuterApply` | **By design** |
| `AsSubQuery` | `From(QueryCommand)` (производная таблица); `As` — workstream 38 | **By design / Planned** |
| `TagQuery` (комментарий-метка в SQL) | нет | **Gap** → [`todo_query_tag.md`](../roadmap/todo_query_tag.md) |
| `InlineParameters` (инлайн констант вместо параметров) | нет (всегда параметризация) | **By design** — расходится с дизайном план-кэша |
| `RemoveOrderBy` | нет; билдер строится снизу вверх, порядок задаёт `OrderBy` | **N/A** (не нужно без `IQueryable`-композиции) |
| `JoinHint` / `SubQueryHint` / `TablesInScopeHint` | `QueryCommand.Hint` только statement-level; table hint (SQL Server), index hint | **Gap** → [`todo_hint_variants.md`](../roadmap/todo_hint_variants.md) |
| `WithTableExpression` / runtime-переопределение `TableName`/`SchemaName`/`ServerName` | `From("table")` + `TableAlias`; `BulkInsertOptions.TableName` (только bulk) | **Gap** → [`todo_source_override.md`](../roadmap/todo_source_override.md) |

Источник списка — публичная поверхность `LinqExtensions` (`Source/LinqToDB/LinqExtensions.cs`); отсутствие
в nextorm проверено по `src/**` (совпадений `TagQuery`/`InlineParameters`/`JoinHint`/`SubQueryHint`/
`TablesInScopeHint`/`WithTableExpression` нет) и по `capability-matrix.md` (строки hints покрывают только
statement/table/index).

### Инфраструктура linq2db (Data Connection System / Mapping / Query Processing)

Отдельная ось: не SQL-конструкции и не операторы, а «обвязка» linq2db. В §1–§3 не попадала по той же
причине (issue-driven развёртка). Проверено по докам linq2db и nextorm (`src/**`, `docs/**`).

| Возможность linq2db (infra) | nextorm | Статус |
|---|---|---|
| `DataConnection`/`DataContext`, владение соединением, повторное использование, dispose | `GetConnection`/`EnsureConnectionOpen`, owned/supplied connection ([Connections and logging](../../guide/16-connections-and-logging.md)) | **Паритет** |
| Транзакции: `BeginTransaction`, commit/rollback, enlist во внешнюю | `ITransactionManager`, `UseTransaction` ([Transactions](../../guide/25-transactions.md)) | **Паритет**; savepoints/вложенность вне поверхности (у linq2db явного API тоже нет) |
| Интерсепторы: command/connection, `BeforeExecute`/`AfterExecute`/`Error` | `IQueryInterceptor` (`CommandInitialized/Executing/Executed(elapsed)/Failed`), `IConnectionInterceptor` ([Interceptors](../../guide/27-interceptors.md)) | **Паритет**; трансляция исключений — By design (`try`/`catch`) |
| Трассировка/лог: `OnTraceConnection`, `TraceInfo`, `TraceSwitch`, `ILogger` | `ILoggerFactory` + `LogSensitiveData` + интерсепторы ([Connections and logging](../../guide/16-connections-and-logging.md)) | **Паритет** |
| Async (транзакции, команды, bulk) | async-терминалы/`EnsureConnectionOpenAsync`/`BeginTransactionAsync` | **Паритет** |
| Compiled queries (`CompiledQuery.Compile`) | `Prepare()` + неявный план-кэш | **By design** |
| `MappingSchema` (несколько/именованные схемы) | один `IEntityMetadata` на тип; scope-override | **Planned** ([`todo_mapping_scope.md`](../roadmap/todo_mapping_scope.md)) |
| Value converters (`IValueConverter`/`SetConverter`) | `ValueConverter<,>`/`[ValueConverter]`/`HasConversion` ([Value converters](../../guide/30-value-converters.md)) | **Паритет** |
| Command timeout (`UseCommandTimeout`/`WithCommandTimeout`) | только `BulkInsertOptions.TimeoutSeconds`; для запросов — через интерсептор | **Gap** → [`todo_command_timeout.md`](../roadmap/todo_command_timeout.md) |
| Dynamic columns (`DynamicColumnsStore`/`DynamicColumnAccessor`) | нет | **Gap** → [`todo_dynamic_columns.md`](../roadmap/todo_dynamic_columns.md) |
| `BulkCopyOptions`-флаги (`CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers`/`BulkCopyType`/parallel) | `BulkInsertOptions` без этих флагов | **Gap** → [`todo_bulk_copy_options.md`](../roadmap/todo_bulk_copy_options.md) |
| Управление кэшем (`Query<T>.ClearCache`, `DisableQueryCache`, `CacheSlidingExpiration`) | per-command `Cache=false`; размер — хвост G10 | **Gap** → [`todo_query_cache_controls.md`](../roadmap/todo_query_cache_controls.md) |
| Оптимизатор дерева (`OptimizeJoins`, `GenerateExpressionTest`) | нет AST-оптимизатора (билдер не `IQueryable`) | **N/A** (архитектурно) |
| DDL/схема (`ITable<T>.Create/Drop`, `CreateLocalTable`) | CTAS; DDL — out-of-scope-решение | **Out-of-scope** (см. §4) |
| Хранимые процедуры / сырой `Execute*` / несколько result-set | `WithSql` (только `SELECT`-источник) | **Planned** ([`todo_stored_procedures.md`](../roadmap/todo_stored_procedures.md), G4-хвост) |
| Association/eager-load, inheritance/TPH | нет метаданных связей | **Out-of-scope** (eager loading — решение gap-analysis §5 п.49) |
| Testing framework, NuGet-упаковка, multi-targeting | собственные тесты/сборка | **N/A** |

## 5. Общие пробелы (нет и у nextorm, и у linq2db)

- **~~Динамическая схема табличных источников~~ — <span style="color:green">Done в nextorm</span> (1.0-b.1).**
  ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`: **<span style="color:green">реализовано</span>**
  явной схемой результата `[SqlTableFunction(..., ResultSchema = …)]` с per-placement capability-гейтом
  (`ISqlDialect.SupportsResultSchema`); схема входит в ключ плана. См.
  [Динамическая схема результата](../../guide/13-table-valued-functions.md#dynamic-result-schema). linq2db это не закрыл
  (`epic: json_sql` его не содержит), так что здесь nextorm впереди.
- ClickHouse `AggregateFunction`-state (`-State`/`-Merge`, `runningAccumulate`) — **единственный
  оставшийся общий пробел**, заблокирован драйвером `ClickHouse.Driver` 1.4.0 у обоих (issue nextorm #62
  снят с майлстоуна):
  [`todo_clickhouse_aggregate_function_state.md`](../roadmap/todo_clickhouse_aggregate_function_state.md).

## 6. Сводка рекомендаций

| Приоритет | Действие |
|---|---|
| — | ~~G1~~ — **<span style="color:green">реализовано</span>** (1.0-b.1, фазы 1–2): [Value converters](../../guide/30-value-converters.md); ~~G2~~ — **<span style="color:green">реализовано</span>** (1.0-b.1, фазы 1–2): [Value converters и JSON-колонки](../../guide/30-value-converters.md) |
| — | ~~G4~~ — **<span style="color:green">реализовано</span>** (1.0-b.1, кроме multi-result): `OUTPUT INTO`/upsert-with-output ([`todo_output_into.md`](../roadmap/todo_output_into.md)); композируемый `INSERT ... RETURNING` (~~G3~~, PostgreSQL) — **<span style="color:green">реализовано</span>**: `MutationCteQuery` + [guide 09](../../guide/09-cte.md#data-modifying-cte-postgresql)/[guide 19](../../guide/19-insert-statement.md#data-modifying-cte-postgresql); остаётся хвост «несколько result-set'ов» (существующий `BatchRunner`, отдельного API нет) |
| P0 | Bulk insert + ~~G5~~ (returning/ignore/identity/chunking) — **<span style="color:green">реализовано</span>**: [Массовая вставка](../../guide/24-bulk-insert.md) |
| — | ~~G6~~ — **By design**: паттерн оптимистичной конкурентности задокументирован ([гайд](../../guide/29-optimistic-concurrency.md)); `todo_optimistic_concurrency.md` не заводится |
| P1 | ~~G7 Regex~~ — **<span style="color:green">реализовано</span>**: [Регулярные выражения](../../ru/scalar-functions/01-string-functions.md#регулярные-выражения); ~~G9 Duration~~ — **<span style="color:green">реализовано</span>** (1.0.6-alpha): [Duration-колонки](../../guide/26-duration-columns.md); ~~G11 PostgreSQL range/`Overlaps`~~ — **<span style="color:green">реализовано</span>** (1.0-b.1): [PostgreSQL-specific SQL](../../guide/provider-specific/postgresql.md#range-types); [`todo_tvp.md`](../roadmap/todo_tvp.md) (G8) |
| P1 | Хранимые процедуры/функции + `OUT`/несколько result-set (снять `limitations.md`, туда же сырые параметризованные команды) → [`todo_stored_procedures.md`](../roadmap/todo_stored_procedures.md) |
| P1 | ~~Логирование параметров~~ (G10) — **<span style="color:green">реализовано</span>** через интерцепторы ([гайд 27](../../guide/27-interceptors.md)); остаётся LRU/размер `DataContextCache` (`MapperCache` уже ограничен); version-gates MariaDB13/PG9.2-9.3 (G13); ~~string-семантика (G12)~~ — **<span style="color:green">реализовано</span>**: [Ordinal-сравнение и коллация](../../ru/scalar-functions/01-string-functions.md#ordinal-сравнение-и-коллация) |
| P1 | ~~Багфикс `date_diff` (G20)~~ — **<span style="color:green">реализовано</span>** аддитивно: `date_diff_big → long?` + `ISqlDialect.MakeDateDiffBig` (см. G20) |
| P2 | ~~G16~~ — не подтвердилось (уже было реализовано), регресс-тесты добавлены (SQL-gen SQLite/PG; интеграция SQLite/PG/MySQL; SQL Server требует `UNION ALL`); ~~G17~~ — **<span style="color:green">реализовано</span>** (интерфейсные коллекции + явный отказ от неподдерживаемого индексера → [Filtering](../../guide/02-filtering-where.md#captured-collection-lookup-dictcolumn)); ~~G18~~ — багфикс `WITH … UPDATE`/`DELETE` закрыт (CTE хойстится перед мутацией, любой join, рекурсивный CTE); ~~G15~~ — проверено, реализовано (PG JSONPath); G14 — проверить |
| P2 | Слепое пятно shipped-`LinqExtensions` (§4): заведены [`todo_query_tag.md`](../roadmap/todo_query_tag.md), [`todo_hint_variants.md`](../roadmap/todo_hint_variants.md), [`todo_source_override.md`](../roadmap/todo_source_override.md) |
| P2 | Инфраструктурные Gap (§4): [`todo_command_timeout.md`](../roadmap/todo_command_timeout.md), [`todo_dynamic_columns.md`](../roadmap/todo_dynamic_columns.md), [`todo_bulk_copy_options.md`](../roadmap/todo_bulk_copy_options.md), [`todo_query_cache_controls.md`](../roadmap/todo_query_cache_controls.md) |
| — | Принять явное решение по **DDL** (оставить out-of-scope или новый workstream) |

## 7. Статус сравнения (обновлено `2026-09-25`)

Устранено: [`linq2db-comparison.md`](linq2db-comparison.md),
[`capability-matrix.md`](capability-matrix.md) и RU-зеркало больше не помечают `UPDATE`, полный `MERGE`,
массовую вставку, CTAS и транзакции как «не реализованы» — они приведены в соответствие с
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4/§6 и кодом в дереве.

Закрыты (в этом gap-анализе): **~~G3~~** — композируемый `INSERT ... RETURNING` через data-modifying CTE на
PostgreSQL (`MutationCteQuery<TResult>`); **~~G5~~** — массовая вставка (returning/ignore/identity/chunking);
**~~G18~~** — `WITH ... UPDATE`/`WITH ... DELETE`. **~~G16~~** — не подтвердилось: уже было реализовано.
**~~G6~~** — **By design** (не gap): при отсутствии change tracking write-back токена не является операцией
фреймворка; явные примитивы покрывают паттерн, поведение задокументировано в
[гайде](../../guide/29-optimistic-concurrency.md).

Дополнительно закрыты/сняты (обновлено `2026-09-25`): **~~G7~~** — `Regex` в запросе (включая SQL Server
2025+ `REGEXP_LIKE`/`REGEXP_REPLACE`)
([Регулярные выражения](../../ru/scalar-functions/01-string-functions.md#регулярные-выражения));
**~~G9~~** — `[Duration]`/`TimeSpan`-колонки ([Duration-колонки](../../guide/26-duration-columns.md));
**~~G12~~** — C# string-семантика (ordinal-трансляция + `string.Format`), вместе с колонковой коллацией
nextorm `#28` ([Ordinal-сравнение и коллация](../../ru/scalar-functions/01-string-functions.md#ordinal-сравнение-и-коллация));
**~~G15~~** — PG JSONPath уже реализован (`jsonb_path_*`, `jsonpath(cast)`, TVF); **~~G10~~** (логирование
параметров) — через интерцепторы (фаза 1, [гайд 27](../../guide/27-interceptors.md)); **~~linq2db#5965~~**
— sub-day-операнд продвигается `ISqlDialect.PromoteDateOperand`
([Duration columns](../../guide/26-duration-columns.md)§9.4); **~~G20~~** — ширина `date_diff` закрыта
аддитивно (`date_diff_big → long?`, `ISqlDialect.MakeDateDiffBig`).

Закрыто майлстоуном **1.0-b.1** (обновлено `2026-09-25`): **~~G1~~** — value converters, фазы 1–2
([Value converters](../../guide/30-value-converters.md)); **~~G2~~** — JSON-колонка ↔ CLR-объект
([Value converters](../../guide/30-value-converters.md)); **~~G4~~** — `OUTPUT INTO` + upsert-with-output
([`todo_output_into.md`](../roadmap/todo_output_into.md); multi-result — хвост); **~~G11~~** — PostgreSQL
range/`Overlaps` ([PostgreSQL-specific SQL](../../guide/provider-specific/postgresql.md#range-types)) и
range поверх пары скалярных колонок (`[RangeColumns]`, [Range columns](../../guide/31-range-columns.md));
  **~~§5 Dynamic result schema~~** — ClickHouse `values()`/PostgreSQL `jsonb_to_record(set)`
  ([Dynamic result schema](../../guide/13-table-valued-functions.md#dynamic-result-schema)). **~~G17~~** — индексатор
captured-коллекции, включая интерфейсные типы; неподдерживаемый индексер (серверный массив/JSON)
отвергается явно ([Filtering](../../guide/02-filtering-where.md#captured-collection-lookup-dictcolumn)).
Единственный общий с linq2db оставшийся пробел — ClickHouse `AggregateFunction`-state (§5), заблокирован
драйвером.

**Добавлено (25.09.2026):** в §4 заведена подсекция «Пропущенная ось: shipped-поверхность
`LinqExtensions`» — закрывает слепое пятно issue-driven развёртки, из-за которого выпущенные операторы
linq2db (`SelectMany`/`GroupJoin`/`DefaultIfEmpty`/`AsSubQuery`) и его hint/table-расширения не попадали
ни в §1–§3, ни в `sql-capabilities-gap-analysis.md`. `SelectMany`/`GroupJoin` классифицированы **By design**
(эквивалент — `CrossApply`/`OuterApply`/`LeftJoin`, SQL-провайдеры бросают `NotSupportedException`);
новые **Gap**-пункты — `TagQuery`, `JoinHint`/`SubQueryHint`/`TablesInScopeHint` и per-query
переопределение источника (`WithTableExpression`/`TableName`). По этим трём Gap-пунктам заведены
рабочие планы [`todo_query_tag.md`](../roadmap/todo_query_tag.md),
[`todo_hint_variants.md`](../roadmap/todo_hint_variants.md),
[`todo_source_override.md`](../roadmap/todo_source_override.md).

**Добавлено (25.09.2026, инфраструктура):** в §4 заведена вторая подсекция — сравнение инфраструктуры
linq2db (Data Connection System / Mapping / Query Processing). Паритет подтверждён по соединениям,
транзакциям, интерсепторам, трассировке/логированию, async, value converters и `Prepare()`. Найдены и
заведены ещё четыре Gap: [`todo_command_timeout.md`](../roadmap/todo_command_timeout.md),
[`todo_dynamic_columns.md`](../roadmap/todo_dynamic_columns.md),
[`todo_bulk_copy_options.md`](../roadmap/todo_bulk_copy_options.md),
[`todo_query_cache_controls.md`](../roadmap/todo_query_cache_controls.md).

## See also

- [`linq2db-comparison.md`](linq2db-comparison.md) — паритет по готовому функционалу.
- [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) — пробелы по SQL-конструкциям.
- [`capability-matrix.md`](capability-matrix.md) — построчная матрица nextorm/EF/linq2db.
- [Limitations](../../advanced/limitations.md) — границы scope.
- Источник среза: `gh issue list -R linq2db/linq2db --state open --limit 1000 --json number,title,labels,milestone` (2026-09-25).
