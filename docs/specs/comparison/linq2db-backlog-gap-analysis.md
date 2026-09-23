# Gap-анализ: открытый backlog linq2db против nextorm

> Источник: живой срез GitHub `linq2db/linq2db` на **2026-09-23** — 381 открытый issue, 8 активных
> `epic:`-меток и milestones `7.0.0`, `6.6.0`, `6.x`, `Backlog`, `In-progress`, `6.5.1`.
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
| **Done** | nextorm уже это умеет (подтверждено кодом/доками/тестами) |
| **Planned** | есть `todo_*.md`; в этом документе не является новым пробелом |
| **Gap** | реальный недостающий функционал в scope nextorm; кандидат на `todo_*.md` |
| **Out-of-scope** | осознанная граница nextorm (не «пробел»); см. [Limitations](../../advanced/limitations.md) |
| **N/A** | внутренняя механика linq2db, баг конкретного провайдера или экосистема вне scope |

Обновление статусов nextorm — на `2026-09-23`: `INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, returning/
output, CTAS и in-memory key-upsert считаются сделанными по
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4/§6 (в
`comparison/`-документах эти строки ещё старые — см. «Расхождения» в конце).

## 1. Epic-уровень: куда линq2db вкладывается

| Epic linq2db | Откр. | Что это | Статус nextorm | Решение |
|---|---|---|---|---|
| `epic: DDL` | 18 | `CREATE`/`ALTER`/`DROP TABLE`, constraints, индексы, sequences, enums, `Create/Drop Database` | CTAS есть (`ToTempTable`/`ToTable`), управления схемой нет | **Gap (решение нужно)**: либо осознанно out-of-scope, либо новый workstream «DDL» |
| `epic: code-generator` | 21 | CLI/T4-скаффолдинг маппингов из живой БД | none (маппинги только в коде) | **Out-of-scope** (заявленная граница) |
| `epic: eager-load` | 12 | `[Association]`, `LoadWith`, `Include`, ordering/strategy | none (только явные join'ы) | **Out-of-scope** (нет метаданных связей) |
| `epic: insert` | 16 | полнота INSERT/UPSERT, bulk, output | `INSERT VALUES/SELECT`, key-upsert, full MERGE — Done; bulk — Planned | смешанно: см. §4 |
| `epic: json_sql` | 5 | JSON-типы, авто-сериализация объектов, `jsonpath`, SQLite TVF | PG native JSON + text-JSON + ClickHouse — Done; объект↔JSON и `jsonpath` — нет | **Gap** (G2) |
| `epic: merge` | 5 | MERGE: immutable-модели, частичные setters, TPH/EF | full MERGE (SQL Server, PG15+) — Done; inheritance/EF — out-of-scope | частично **Gap** (G-merge) |
| `epic: output` | 6 | `OUTPUT`/`OUTPUT INTO`, несколько result-set'ов, INSERT…WithOutput в CTE | returning/output одного стейтмента — Done | **Gap** (G4) |
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
| `#5759` | `TimeSpan`-члены и сравнения на native interval-колонках (`[Duration]`) | **Gap** (G9) → [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md) |
| `#5933` | диалект MariaDB 13 | **Gap** (G13) |
| `#5948`, `#5952` | PG 9.2/9.3: `FILTER`-агрегаты синтаксически недоступны | **Gap** (G13, version-gate) |
| `#5961`, `#5914` | ClickHouse date/`DateTimeOffset` типы в SQL противоречат декларации | **Проверено, не gap.** `#5961`: у nextorm нет per-node `DbDataType`, а части даты уже приведены к объявленному CLR-типу (`toInt32(toISOWeek(toDateTime64(…)))`, `toInt32(toYear(…))`, `toFloat64(toUnixTimestamp(…))`); `to_unix_timestamp` объявлен `long` и обёрнут `toInt64` — «type lie» не воспроизводится. `#5914`: `DateTimeOffset` в nextorm не маппится вовсе (`SelectExpression.GetDataRecordMethod` бросает), поэтому расхождение Date/DateTime64 отсутствует — **N/A**. Однотипный хвост — ширина `date_diff`, см. G20; общий план — [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md) §8 |
| `#5921` | `string.Format`/интерполяция: format-спецификаторы молча теряются | **Gap** (G12) |
| `#5927` | `string.CompareOrdinal`/ordinal `Compare` маппятся в culture-sensitive | **Gap** (G12) |
| `#5965` | sub-day date-функции над date-only операндами | **Gap (мелкий).** nextorm не хранит SQL-тип/точность колонки (везде CLR `DateTime`) и не продвигает операнд: `date_add`/`DateTime.Add*` и `.Hour/.Minute/.Second` рендерятся прямо на колонке (`SqlServerDialect.cs:351` → `dateadd(millisecond, n, value)`, `ClickHouseDialect.cs:531` → `addMilliseconds(value, n)`). На SQL Server `date`-колонка даёт 9810 (замер linq2db), на ClickHouse `Date` sub-day часть, вероятно, молча теряется — класс `#5955`/`#5959`; PG/MySQL/SQLite через interval/`datetime()` безопасны. `DateTime.Millisecond` вообще не маппится. Фикс в духе linq2db — продвинуть операнд к дробному timestamp (`datetime2`/`toDateTime64`) перед sub-day функцией, но нужно знать precision → смежно с G9; вынесено в [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md) §8.2 |
| `#5837`, `#5838`, `#5852` | inheritance/TPH write, shadowing-член | **Out-of-scope** (нет TPH) |
| `#5904`, `#5937`, `#5941`, `#5940`, `#5865` | eager-load ordering/strategy | **Out-of-scope** |
| `#5717` | DML `RETURNING`/`OUTPUT` как **композируемый** `IQueryable`-источник | **Gap** (G3) |
| `#4562` | PostgreSQL `Overlaps` (range `&&`) | **Gap** (G11) → [`todo_postgres_ranges.md`](../roadmap/todo_postgres_ranges.md) |
| `#4543` | декларативный `QueryFilter`-атрибут | **Out-of-scope** (нет query filters) |
| `#5706`, `#1879`, `#3740`, `#5425` | Oracle-специфика | **Out-of-scope** (нет провайдера) |
| `#3023`, `#5895`–`#5897` | Sybase/DB2-специфика | **Out-of-scope** |
| `#4745`, `#5081`, `#5911`, `#5903`, `#5861`, `#5862`, `#5730` | внутренности/SQL-gen/упаковка | **N/A** |

### `Backlog` (44, «Available tasks») — фичи, не привязанные к релизу

| linq2db | Тема | Статус nextorm |
|---|---|---|
| `#698` | `Regex` внутри запроса (трансляция `Regex.IsMatch`/…) | **Gap** (G7) → [`todo_regex.md`](../roadmap/todo_regex.md) |
| `#1645` | table-valued **parameters** для хранимых процедур (TVP) | **Gap** (G8) → [`todo_tvp.md`](../roadmap/todo_tvp.md); смежно [`todo_stored_procedures.md`](../roadmap/todo_stored_procedures.md) |
| `#1994` | open-generic `TypeConverter` | **Gap** (G1) |
| `#3009` | ограничение размера кэша запросов | **Gap** (G10) |
| `#4039` | логирование SQL-параметров | **Planned** (`todo_interceptors.md`) |
| `#4405` | concurrency check с явными исключениями | **Gap** (G6) |
| `#4199` | «Property X is not defined for interface type Y» | **Planned** (`todo_interface_poco.md`) |
| `#5822` | рекурсивный CTE с `UNION` и вычисляемой проекцией | **Проверено, не gap** (G16): union рендерится inline, self-ref не оборачивается |
| `#5879` | `IndexExpression` (`x[i]`) в дереве запроса | **Gap** (G17): `x[i]` → `NotSupportedException`; hand-built → молча неверный SQL |
| `#3015` | `UPDATE` через CTE (несколько провайдеров) | **Gap (баг)** (G18) → [`todo_cte_in_update.md`](../roadmap/todo_cte_in_update.md): `WITH ... UPDATE` не эмитится, CTE теряется |
| `#4139` | composite-объекты для associations | **Out-of-scope** |
| `#1181`, `#1651`, `#3914`, `#4314`, `#5527`, `#5853`, `#5903` | типы/mapping/инфраструктура | **N/A** |
| остальные (`#86`, `#1880`, `#4436`, …) | багфиксы провайдеров | **N/A** |

### `6.5.1` (7) и `In-progress` (5) — корректность
Почти всё — регрессии `PreferClientCalculation`/eager-load/провайдеров (**N/A**), кроме `#698` и
`#4306`/`#2950` (`TimeSpan`-типы на SQL Server/PostgreSQL) → см. **G9**.

### Что linq2db уже **выпустил** в 6.5.0 и чего у nextorm нет
Не backlog, но свежий вектор (release notes wiki):
`UpdateOptimisticWithRefresh` (оптимистичный update с write-back токена) → **G6**;
`[Duration]`/`TimeSpan`-колонки → **G9**; `BulkCopyOptions.MaxSqlLengthForBatch` → относится к
`todo_bulk_insert.md`; F# `option`/single-case discriminated unions → **Out-of-scope** (F#);
LINQPad/gRPC-remote-context → **Out-of-scope**.

## 3. Приоритезированные in-scope пробелы

### P0 — усиливают уже выбранное направление (writing/JSON/bulk)

**G1. Value converters / кастомный маппинг типов.** `linq2db#1994` (open generic) и общая тема
`area: mapping`/`area: types`. В nextorm сегодня **нет** конвертеров свойств: `IPropertyMetadata`
(`src/nextorm.core/DataContext/Meta/IPropertyMetadata.cs:17-57`) знает только
`PropertyInfo`/`ColumnName`/`IsKey`/`IsIdentity`/`IsComputed`. Без этого JSON-колонки, enum-as-string,
`DateTimeOffset`-в-строках и кастомные типы требуют ручных проекций.
Действие: новый [`todo_value_converters.md`](../roadmap/todo_value_converters.md); точка расширения — `IPropertyMetadata` + fluent mapping
(`EntityPropertyBuilder<T>`) + reader/writer-сиде (`RowMapperFactory`, `SqlMutationBuilder`).

**G2. JSON-колонка ↔ CLR-объект (авто-сериализация).** `linq2db#1661`. nextorm умеет читать/писать
native JSON (PG) и text-JSON функции, но не «свойство типа `MyDto` хранится в JSON-колонке».
Опирается на G1. Действие — [`todo_json_column_mapping.md`](../roadmap/todo_json_column_mapping.md): расширить `SupportedJson`/`IPropertyMetadata` (атрибут `[JsonColumn]`),
сериализация через `System.Text.Json`; связать с [`todo_json_streaming.md`](../roadmap/todo_json_streaming.md) (общий STJ-слой).

**G3. DML `RETURNING`/`OUTPUT` как композируемый источник.** `linq2db#5717` (milestone `6.x`).
Сейчас nextorm отдаёт записанные строки терминалом `Returning(...)`, но не даёт читать их дальше как
подзапрос/источник `FROM` (на PG уже есть data-modifying CTE — обобщить). Действие: `todo_dml_returning_source.md`
или расширение публичной DML-поверхности ([guide 19](../../guide/19-insert-statement.md)).

**G4. `OUTPUT INTO` + несколько result-set'ов + upsert-with-output.** `linq2db#3832` (`...WithOutputIntoOutput`),
`#2982` (multi-result-set), `#3124`/`#4824` (InsertOrUpdate/InsertOrActionWithOutput). nextorm имеет
`OUTPUT`/`RETURNING` для одиночных `INSERT`/`UPDATE`/`DELETE`, но не `OUTPUT INTO <table>`, не
несколько наборов и не output у key-upsert. Действие — [`todo_output_into.md`](../roadmap/todo_output_into.md).

**G5. Bulk-insert: возврат идентификаторов и конфликтная политика.** `linq2db#2960` (Returning IDs),
`#5124` (Bulk Insert Ignore), `#3795` (identity insert). **Отражено** в
[`todo_bulk_insert.md`](../roadmap/todo_bulk_insert.md): возврат ключей — через batch `INSERT ... VALUES
... RETURNING`/`OUTPUT` (нативный bulk ключей не даёт), `IgnoreDuplicates()` на диалектных хуках
`SupportsOnConflict`/`MakeOnConflict` (+ новый ignore-хук для MySQL/SQL Server), `KeepIdentity()`
(провайдерные формы) и чанкинг `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`
(`MaxSqlLengthForBatch`-аналог).

**G6. Оптимистичная конкурентность.** `linq2db#4405` и shipped `UpdateOptimisticWithRefresh` (6.5.0).
Явный API в духе nextorm (без change tracking): `Update<T>().IfUnchanged(x => x.RowVersion, value)`,
`InsertOrUpdate` с write-back токена, `0` строк = конфликт. Действие: `todo_optimistic_concurrency.md`.
Замечание: nextorm уже возвращает число затронутых строк (`ExecuteNonQuery`), так что кирпичик есть.

### P1 — расширение паритета

**G7. `Regex` в запросе.** `linq2db#698` (`area: extensions`, `area: sql`). nextorm умеет `LIKE`-семейство,
но не `Regex.IsMatch`/`Regex.Replace`. Действие: [`todo_regex.md`](../roadmap/todo_regex.md) (per-provider: PG `~`, MySQL `REGEXP`,
SQL Server — без native (CLR/`LIKE`-rewrite), SQLite `REGEXP`, ClickHouse `match`).

**G8. Table-valued parameters (TVP).** `linq2db#1645`. nextorm умеет TVF как **источник**, но не
передачу таблицы **параметром** (SQL Server `SqlParameter` с structured type; PG — array/`unnest`).
Действие: [`todo_tvp.md`](../roadmap/todo_tvp.md).

**G9. `TimeSpan`/interval-колонки (`[Duration]`).** `linq2db#5759`, `#4306`, `#2950`. nextorm делает
date arithmetic, но не хранит/сравнивает `TimeSpan` как interval с объявленной единицей.
Действие: [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md).

**G10. Ограничение кэша планов + логирование параметров.** `linq2db#3009` (cache size), `#4039`
(parameter logging). Первое — риск неограниченного роста process-wide кэшей (`MapperCache`,
`DataContextCache`); второе частично в [`todo_interceptors.md`](../roadmap/todo_interceptors.md).
Действие: добавить LRU/размер в кэш-ключ-инфраструктуру; в логирование — параметры.

**G11. PostgreSQL range/`Overlaps`.** `linq2db#4562`; shipped `Sql.Row.Overlaps` (6.5.0). nextorm не
имеет range-типов (`tsrange`, `daterange`, `&&`). Действие: [`todo_postgres_ranges.md`](../roadmap/todo_postgres_ranges.md) (PG-only surface).

**G12. C# string-семантика.** `linq2db#5921` (format-спецификаторы в `string.Format`/`$"…"`),
`#5927` (ordinal `Compare`/`CompareOrdinal` сворачиваются в culture-sensitive `CompareTo`). В nextorm
`string.Compare*`/`string.Format`/интерполяция **не транслируются вовсе** (`StringFunctionTranslator.cs:30-75`
их не содержит), поэтому сегодня это не «тихо неверно», а «не поддержано» — нужен явный тест-контракт.
Важно: корректный ordinal-вариант `#5927` требует явной binary/ordinal-коллации на сравнении — тот же
примитив, что нужен колонковой коллации (nextorm issue `#28` «column collation»; в nextorm коллаций нет
вообще). Связанные, но **разные** задачи: G12 — верность трансляции C#-семантики, `#28` — схема/маппинг.
Действие: реализовывать с общим collation-примитивом; отдельный `todo` не обязателен.

**G13. Версионные диалекты и version-gates.** `linq2db#5933` (MariaDB 13), `#5948`/`#5952`
(PG 9.2/9.3 не поддерживают `FILTER` в агрегатах). nextorm генерирует `FILTER`-агрегаты на PG и
имеет единый MariaDB-диалект без версии. Действие: version-flag в `ISqlDialect`/`MariaDbDialect`,
аналогично существующим `Supports*`.

### P2 — watchlist / проверить

| # | linq2db | Что проверить в nextorm |
|---|---|---|
| G14 | `#3408` SQLite `json_each`/`json_tree` | нет built-in TVF; выразимо `[SqlTableFunction]`-обёрткой — решить, нужен ли built-in |
| G15 | `#3869` PG `jsonb` jsonpath | есть native JSON на PG; покрыт ли `jsonb_path_query`/`@?` за пределами TVF |
| G16 | `#5822` recursive CTE `UNION` + вычисляемая проекция | **Проверено, не gap** — union inline, self-ref не оборачивается; distinct-`Union` покрыт тестами (SQLite/PG/MySQL; SQL Server отвергает — требует `UNION ALL`) |
| G17 | `#5879` `IndexExpression` в дереве | **Gap (мелкий)** — `x[i]` из C# → `NotSupportedException: ArrayIndex`; hand-built → молча неверный SQL (см. ниже) |
| G18 | `#3015` `UPDATE` через CTE | **Gap (баг)** — `WITH ... UPDATE` не эмитится, CTE теряется (см. ниже) |
| G19 | `#5675`/`#5718`/`#5758` | архитектурный ориентир для метаданных/кэша, не gap |

**G16. Рекурсивный CTE с `UNION` (distinct) и вычисляемой проекцией.** `linq2db#5822`. Проверено
генерацией на SQLite: `WithRecursive("nums", anchor.Union(step))` с внешней проекцией `n * 2` даёт
`with recursive nums as (select … union select … from nums …) select (n * 2) as 'n' from nums` — union
рендерится **inline** (`SqlBuilder.MakeSelect`, ветка `UnionQuery`, `SqlBuilder.cs:280`), derived-table
обёртки, как в linq2db, нет, self-reference остаётся на разрешённом месте. **Не gap** — архитектурное
отличие: у nextorm нет оптимизатора, складывающего проекцию в ноги set-операции. Покрытие добавлено:
SQL-gen — `tests/nextorm.sqlite.tests/SqlGenerationTests.cs` и `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
(`Cte_Recursive_WithDistinctUnion_ShouldEmitUnionNotUnionAll`), интеграция per-provider —
`SqliteSpecificTests`/`PostgresSpecificTests`/`MySqlSpecificTests`
(`Cte_Recursive_WithDistinctUnion_ShouldProduceNumberSeries`), т.к. **SQL Server отвергает**
рекурсивный CTE с top-level distinct `UNION` («Recursive common table expression 'nums' does not
contain a top-level UNION ALL operator» — проверено интеграцией), поэтому тест не в общем
`CommonTestSuite`. На T-SQL distinct-`Union` в `WithRecursive` даёт невалидный SQL — кандидат на gate.

**G17. `IndexExpression`/`x[i]` в дереве.** `linq2db#5879`. Два пути, оба неполные:
- обычный C# `values[0]` (компилятор → `BinaryExpression`/`ArrayIndex`) бросает
  `NotSupportedException: ArrayIndex` в `PredicateTranslator.VisitBinary`
  (`src/nextorm.core/Visitors/PredicateTranslator.cs:400`);
- hand-built `Expression.ArrayAccess` (`IndexExpression`) идёт в `BaseExpressionVisitor.VisitIndex`
  (`BaseExpressionVisitor.cs:409`) → `MemberTranslator.VisitIndex` (`MemberTranslator.cs:474`), который
  обрабатывает только внутренний `QueryCommand`-индекс (скалярный подзапрос) и для остального вернёт
  `null` → `base.VisitIndex` посещает объект/аргументы, из-за чего значение **молча** биндится как
  параметр-массив (`somestring = $values0`), а индекс теряется — хуже явной ошибки linq2db.
nextorm поддерживает массив как **целый** операнд (`= any(@arr)`), но не поэлементный доступ. Действие:
транслировать константный индекс в скалярный параметр (и/или в `arr[i+1]` на провайдерах с массивами),
либо явно отклонять; минимум — не отдавать молча неверный SQL. Тест отсутствует.

**G18. `WITH ... UPDATE`.** `linq2db#3015`. Проверено генерацией на SQLite (`SupportsUpdateJoin`):
join цели к CTE-источнику `ctx.With("c", q).From("c")` даёт
`update simple_entity as 't1' set id = $p0 from c as 't2' where t1.id = t2.id` — **без** `with c as (…)`,
т.е. невалидный SQL. CTE теряются: `UpdateJoinCommand`/`UpdateBuilder` не несут `Ctes`, а
`JoinedEntityBuilder` берёт их только с левой стороны. Сама `UPDATE ... FROM`/join работает (тесты
`UpdateJoinSqlGenerationTests.cs`, `UpdateJoin_ShouldChangeTargetFromJoinedRow`), но CTE в неё не
пробрасывается. Это не та задача, что `#3015` (там CTE в UPDATE уже работает и лишь требует переноса в
source на части провайдеров) — здесь пробел глубже: CTE не эмитится вообще. Действие: прокинуть `Ctes`
в мутационные `*Command` и хойстить `WITH` в `QueryPlanner.RenderUpdateJoin` по образцу `RenderSource`
(`QueryPlanner.cs:95`); тест — SQL-gen + интеграция (PG/SQLite). План — [`todo_cte_in_update.md`](../roadmap/todo_cte_in_update.md).

**G20. Ширина результата `date_diff` (найдено при проверке `#5961`).** `CommonFunctions.date_diff`
(`src/nextorm.core/Query/SqlFunctions.cs:525`) объявлен `int?`, но ClickHouse рендерит
`dateDiff('unit', …)` → **Int64**, а `SelectExpression.GetDataRecordMethod()` читает его как `GetInt32`.
Для `seconds`/`milliseconds`/`microseconds` значение легко превышает Int32 (~24,8 дня для мс, ~35,8 мин
для мкс). SQL Server/PG base отдают `int` (PG кастит `… as integer` явно) — там расхождения нет;
MySQL/MariaDB `timestampdiff` — тот же класс (ширину проверить). Это не gap фичи, а багфикс: либо
расширить декларацию до `long?` (публичный API → `API-NAMING-REVIEW` + доки EN/RU), либо явно ограничить
набор полей/сузить вывод на ClickHouse. Нужен тест ClickHouse `date_diff` (сейчас отсутствует —
покрыты только PG/MySQL/SQL Server/SQLite SQL-gen и `day`-интеграция). Общий план — [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md) §8.3.

## 4. Осознанно вне scope (не считать пробелами)

Совпадает с [`linq2db-comparison.md`](linq2db-comparison.md) «Deliberate boundaries»:

- **DDL/управление схемой** (`epic: DDL`): `CREATE`/`ALTER`/`DROP TABLE`, constraints, индексы, sequences,
  enums, `Create/Drop Database`. nextorm мутирует схему только через CTAS. Это **единственный крупный
  непокрытый эпик**, где нужно явное решение: осознанно оставить или завести workstream «DDL» (в
  `sql-capabilities-gap-analysis.md` DDL сейчас в «Future workstreams (not scheduled)»).
- **Связи/eager-load** (`epic: eager-load`) и **inheritance/TPH**.
- **Скаффолдинг/кодогенерация** (`epic: code-generator`).
- **Новые провайдеры** (`epic: new-provider`): Oracle, Firebird, DB2, SAP HANA, Informix, Sybase,
  Redshift, DuckDB, YDB, Access, SQL CE.
- **Remote context / gRPC**, **LINQPad-драйвер**, **F#-специфика**.
- **Change tracking / identity map**, **query filters**.
- **EF Core integration** — не out-of-scope, а `Planned` ([`todo_efcore_integration.md`](../roadmap/todo_efcore_integration.md));
  `linq2db#4044`, `#4611`, `#4666` закрываются ей.

## 5. Общие пробелы (нет и у nextorm, и у linq2db)

- Динамическая схема табличных источников: ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)` —
  [`todo_dynamic_result_schema.md`](../roadmap/todo_dynamic_result_schema.md). linq2db это тоже не закрыл
  (`epic: json_sql` его не содержит).
- ClickHouse `AggregateFunction`-state (`-State`/`-Merge`) — заблокировано драйвером у обоих:
  [`todo_clickhouse_aggregate_function_state.md`](../roadmap/todo_clickhouse_aggregate_function_state.md).

## 6. Сводка рекомендаций

| Приоритет | Действие |
|---|---|
| P0 | Завести [`todo_value_converters.md`](../roadmap/todo_value_converters.md) (G1) и [`todo_json_column_mapping.md`](../roadmap/todo_json_column_mapping.md) (G2, зависит от G1) |
| P0 | [`todo_output_into.md`](../roadmap/todo_output_into.md): `OUTPUT INTO`/multi-result/upsert-with-output (G4); composable RETURNING/OUTPUT (G3) — дополнить guide 19 / `MergeBuilder` |
| P0 | `todo_bulk_insert.md` дополнен под G5 (returning IDs, ignore-политика, identity insert, `MaxSqlLengthForBatch`-аналог) |
| P0 | Завести `todo_optimistic_concurrency.md` (G6) |
| P1 | [`todo_regex.md`](../roadmap/todo_regex.md) (G7), [`todo_tvp.md`](../roadmap/todo_tvp.md) (G8), [`todo_timespan_columns.md`](../roadmap/todo_timespan_columns.md) (G9), [`todo_postgres_ranges.md`](../roadmap/todo_postgres_ranges.md) (G11) |
| P1 | Хранимые процедуры/функции + `OUT`/несколько result-set (снять `limitations.md`, туда же сырые параметризованные команды) → [`todo_stored_procedures.md`](../roadmap/todo_stored_procedures.md) |
| P1 | LRU/размер кэшей + логирование параметров (G10); version-gates MariaDB13/PG9.2-9.3 (G13); string-семантика (G12) |
| P1 | Багфикс `date_diff` (G20): ClickHouse Int64 читается как Int32 — решить `long?` vs явное сужение |
| P2 | G16 — регресс-тесты добавлены (SQL-gen SQLite/PG; интеграция SQLite/PG/MySQL; SQL Server требует `UNION ALL`); G17 — точечный багфикс (IndexExpression); G18 — багфикс `WITH … UPDATE` → [`todo_cte_in_update.md`](../roadmap/todo_cte_in_update.md); G14/G15 — проверить |
| — | Принять явное решение по **DDL** (оставить out-of-scope или новый workstream) |

## 7. Расхождения в текущих документах (важно)

Статус DML в `comparison/linq2db-comparison.md` и `comparison/capability-matrix.md` устарел: там
`UPDATE` и полный `MERGE` помечены как «не реализованы», тогда как
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4/§6 и код в дереве
(`src/nextorm.core/Builders/MergeBuilder*.cs`, `SqlMutationBuilder.cs`) считают их сделанными
(SQL Server + PostgreSQL 15+). Перед публикацией этого gap-анализа сравнение стоит пересобрать, чтобы
таблица паритета не противоречила roadmap.

## See also

- [`linq2db-comparison.md`](linq2db-comparison.md) — паритет по готовому функционалу.
- [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) — пробелы по SQL-конструкциям.
- [`capability-matrix.md`](capability-matrix.md) — построчная матрица nextorm/EF/linq2db.
- [Limitations](../../advanced/limitations.md) — границы scope.
- Источник среза: `gh issue list -R linq2db/linq2db --state open --limit 1000 --json number,title,labels,milestone` (2026-09-23).
