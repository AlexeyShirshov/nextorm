# Gap-анализ: открытый backlog linq2db против nextorm

> Источник: живой срез GitHub `linq2db/linq2db` на **2026-09-25** — 382 открытых issue, 8 активных
> `epic:`-меток и milestones `7.0.0`, `6.6.0`, `6.x`, `6.5.1`, `In-progress`. Воспроизведение:
> `gh issue list -R linq2db/linq2db --state open --limit 1000 --json number,title,labels,milestone`.
> База ссылок `linq2db#N` — <https://github.com/linq2db/linq2db/issues/N>.
>
> Назначение: **реестр открытых пунктов**. Здесь только то, чего в nextorm ещё нет, с тикетом/планом
> на каждый пункт. Закрытые пункты и обоснования «почему это не gap» в файле не дублируются: границы
> продукта и их причины — в публичной документации [Limitations и out-of-scope](../../advanced/limitations.md).

## Как читать

| Статус | Значение |
|---|---|
| **Gap** | недостающий функционал в scope nextorm; в строке — тикет |
| **Planned** | есть `todo_*.md` с планом; нового пробела не заводит |
| **Blocked** | цель принята, но внешний блокер (драйвер/сервер) |
| **Out-of-scope** | осознанная граница; решение зафиксировано, обоснование — в публичной документации |
| **Partial** | часть случая покрыта; остаток — осознанная граница, отдельного пункта в §1 нет |
| **Watch** | проверено, влияния на nextorm сейчас нет; держим в поле зрения |

Срез идёт не только по открытым issue, но и по **shipped-поверхности** linq2db
(`LinqExtensions`, Data Connection System / Mapping / Query Processing): issue-driven обход её
пропускает. Результат по этой оси — «эквивалент есть», «покрыто частично» либо «осознанная
граница», поэтому строки сведены к ссылкам на документацию, а не к разбору.

## 1. Открытые пункты

| # | Тема | Источник linq2db | Статус | Тикет / план | Документация |
|---|---|---|---|---|---|
| G21 | SQL Server: legacy `WithRecursive` с top-level **distinct** `UNION` рендерит невалидный T-SQL — нужен fail-closed гейт (typed `AsRecursiveCte` не затронут: он всегда `UNION ALL`) | `#5822` (хвост) | **Gap** | [#265](https://github.com/AlexeyShirshov/nextorm/issues/265) | [CTE](../../guide/10-cte.md) |
| G22 | SQL Server: **композируемый** DML `OUTPUT` (строки `INSERT`/`UPDATE`/`DELETE`/`MERGE` как источник последующего запроса); у PostgreSQL это `MutationCteQuery`, у T-SQL `OUTPUT` в CTE невозможен | `#5717` (хвост) | **Gap** | [#266](https://github.com/AlexeyShirshov/nextorm/issues/266) | [INSERT](../../guide/17-insert-statement.md#data-modifying-cte-postgresql), [SQL batch](../../guide/25-sql-batch.md) |
| G23 | ClickHouse `AggregateFunction(...)`-state: `-State`/`-Merge`, `runningAccumulate` | общий пробел (нет и у linq2db) | **Blocked** драйвером `ClickHouse.Driver` 1.4.0 | [#62](https://github.com/AlexeyShirshov/nextorm/issues/62), [`todo_clickhouse_aggregate_function_state.md`](../roadmap/todo_clickhouse_aggregate_function_state.md) | [Limitations: ClickHouse aggregate state](../../advanced/limitations.md) |
| G24 | Mapping scope — scope-зависимый маппинг одной сущности (аналог нескольких `MappingSchema`) | инфраструктура (`MappingSchema`) | **Planned** | [#65](https://github.com/AlexeyShirshov/nextorm/issues/65), [`todo_mapping_scope.md`](../roadmap/todo_mapping_scope.md) | — |
| G25 | Интерфейсные сущности без класса реализации (runtime POCO) | `#4199` | **Planned** | [#34](https://github.com/AlexeyShirshov/nextorm/issues/34), [`todo_interface_poco.md`](../roadmap/todo_interface_poco.md) | — |
| G26 | Потоковая выдача JSON в `Stream` (общий STJ-слой; сейчас стриминг — CSV/JSON-терминалы с собственными планами) | `#1661` (хвост) | **Planned** | [#39](https://github.com/AlexeyShirshov/nextorm/issues/39), [`todo_json_streaming.md`](../roadmap/todo_json_streaming.md) | [Streaming data](../../guide/30-streaming-data.md) |
| G27 | Составные ключи связей в публичном объявлении + metadata-driven `LoadWith` (включая одноколоночные fluent-помощники объявления) | `epic: eager-load` | **Planned** | [#251](https://github.com/AlexeyShirshov/nextorm/issues/251), [#249](https://github.com/AlexeyShirshov/nextorm/issues/249) | [Relationships](../../relationships/relationships.md), [Eager loading](../../relationships/eager-loading.md) |
| G28 | `DataContextCache`: жёсткий предел размера / LRU (сейчас — только скользящее истечение; `MapperCache` ограничен 4096) | `#3009` (хвост) | **Watch** | — | [Cache controls](../../engine/03-plan-cache-and-reuse.md#cache-controls) |
| G29 | `MERGE`: значения колонок в ветках `ThenUpdate`/`ThenInsert` берутся только из одноимённой колонки source; нужны per-column выражения (target/source) с default из source для остальных — «частичные setters» | `#4357` | **Gap** | [#301](https://github.com/AlexeyShirshov/nextorm/issues/301) | [MERGE](../../guide/21-merge-statement.md) |
| G30 | Bulk insert: остаток linq2db `BulkCopyOptions` — `BulkCopyType`, `UseInternalTransaction`, `ServerName`/`DatabaseName`, schema-only override, `TableOptions`, timeout вне SQL Server; ClickHouse `MaxDegreeOfParallelism`/`WithoutSession` | shipped-поверхность (`BulkCopyOptions`) | **Done** ([#305](https://github.com/AlexeyShirshov/nextorm/issues/305)) — все семь пунктов закрыты, disposition-таблица ниже | [#305](https://github.com/AlexeyShirshov/nextorm/issues/305) | [Bulk insert](../../guide/22-bulk-insert.md), [Limitations](../../advanced/limitations.md) |

## 2. Зафиксированные решения (не открывать заново)

| Тема | Решение | Документация |
|---|---|---|
| DDL / управление схемой (`epic: DDL`, 18 issue): `CREATE`/`ALTER`/`DROP TABLE`, индексы, constraints, sequences, enums, `Create/Drop Database` | **Out-of-scope.** Генерируется только материализация запроса: `CREATE TABLE ... AS SELECT`, `DROP TABLE IF EXISTS`, `TRUNCATE` | [Limitations](../../advanced/limitations.md), [Create table as](../../guide/20-create-table-as.md) |
| Скаффолдинг/кодогенерация из живой схемы (`epic: code-generator`, 21) | **Out-of-scope** — маппинги объявляются в коде | [Limitations](../../advanced/limitations.md) |
| Новые провайдеры (`epic: new-provider`): Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Redshift, DuckDB, YDB, Access, SQL CE | **Out-of-scope** — набор провайдеров фиксирован | [Limitations](../../advanced/limitations.md) |
| Inheritance / TPH (иерархия типов сущностей) | **Out-of-scope** как встроенная функция — переложено на пользователя: один CLR-тип = одна таблица, форму иерархии (TPH/TPT/TPC) и полиморфное чтение моделирует пользователь | [Наследование и производные сущности](../../guide/32-inheritance-and-derived-entities.md), [Limitations](../../advanced/limitations.md) |
| Immutable-модели в `MERGE` (`linq2db#4237`): сеттер строит target целиком через object initializer, поэтому `init`/ctor-only модель невыразима | **Out-of-scope** — сущности плоские; сеттер MERGE задаёт набор колонок, а не конструктор объекта (значения колонок — отдельный G29) | [MERGE](../../guide/21-merge-statement.md) |
| Composite-объекты для associations (`linq2db#4139`) | **Out-of-scope** — сущности плоские; связь описывается колонками и `HasMany`/`HasOne` | [Relationships](../../relationships/relationships.md) |
| F#-специфика (`option`, single-case unions), LINQPad-драйвер, remote/gRPC-контекст | **Out-of-scope** | [Limitations](../../advanced/limitations.md) |
| Change tracking / identity map / `SaveChanges`, `UpdateOptimisticWithRefresh` | **By design** — явные примитивы (`Where` + число строк, `Returning`) и гайд; write-back токена — присваивание в объекте вызывающего кода | [Optimistic concurrency](../../guide/26-optimistic-concurrency.md) |
| `SelectMany` / `GroupJoin` / `DefaultIfEmpty` как самостоятельные операторы | **By design** — `CrossApply`/`OuterApply`/`LeftJoin` + `GROUP BY`; SQL-провайдеры падают закрыто | [Joins](../../guide/04-joins.md), [Limitations](../../advanced/limitations.md) |
| `InlineParameters` (инлайн констант) | **Out-of-scope** — сознательно только параметризация; инлайн-переключатель не планируется. Это не «покрыто»: при необходимости инлайна в nextorm его нет | [Cache controls](../../engine/03-plan-cache-and-reuse.md#cache-controls) |
| `CompiledQuery`-делегат (`CompiledQuery.Compile`) | **Partial** — `Prepare()` покрывает compile-once и повторное исполнение, но возвращает stateful-команду (один `DbCommand` + enumerator) с позиционными параметрами, а не типизированный потокобезопасный `Func<...>`; конкурентное разделение и композиция после `Prepare()` не поддерживаются. Серверный путь — per-thread неявный план-кэш; typed concurrent delegate не планируется | [`Prepare`](../../engine/03-plan-cache-and-reuse.md#explicit-prepare) |
| `AsSubQuery` / именование query-блока | **Partial** — `From(QueryCommand)` и `As<T>` дают производные таблицы, но `AsSubQuery("name")`/`QB_NAME` и произвольное переименование источника не покрыты | [Query sources](../../guide/01-query-sources.md), [Joins](../../guide/04-joins.md) |
| Bulk-copy flags вне SQL Server (`CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers`) | **Closed по семантической матрице** — `TableLock`/`FireTriggers` только нативный SQL Server (`true` → named refusal, `false` → honored отсутствием запроса); `CheckConstraints=false` — нативный SQL Server, MariaDB (scoped-контроль сессии) и SQLite (scoped-прагма вне транзакции), иначе named refusal; `KeepNulls=false` — только default-equivalent форма, иначе named refusal. Ни одна опция не принимается и не игнорируется молча | [Bulk insert](../../guide/22-bulk-insert.md#sql-server-bulk-copy-options), [Limitations](../../advanced/limitations.md) |

### G30 — финальные dispositions (issue #305, закрыт)

| # | Пункт linq2db | Disposition |
|---|---|---|
| 1 | `BulkCopyType` | **implemented** как `BulkInsertMode { Auto, Native, Portable }`, `Auto` по умолчанию; `Default`-совместимости нет, `Native` не откатывается, `Portable` не становится нативным неявно |
| 2 | `UseInternalTransaction` | **implemented** только нативным SQL Server (`SqlServerBulkInsertOptions.UseInternalTransaction` → `SqlBulkCopyOptions.UseInternalTransaction`, покрыт батч драйвера); на других путях/провайдерах — **named refusal**; комбинация с внешней транзакцией — named refusal |
| 3 | `ServerName` / `DatabaseName` | `DatabaseName` **implemented** и dialect-scoped (PostgreSQL — только текущая база, иначе named refusal; остальные — по своей квалификации); `ServerName` — **named refusal** везде: перенаправления на другой сервер нет и не обещается |
| 4 | schema-only override | **implemented**: `SchemaName`/`Schema(name)` меняет только схему, сохраняя замапленную таблицу; равные алиасы `TableSchema`/`SchemaName` схлопываются, различающиеся — ошибка аргумента |
| 5 | `TableOptions` (DDL-флаги) | **implemented** как `BulkInsertTargetKind { Regular, LocalTemporary }` поверх существующего temp-механизма; DDL-флаги (create/drop/alter) — **out-of-scope** |
| 6 | `BulkCopyTimeout` / timeout вне SQL Server | **implemented** как `TimeSpan? Timeout` — один монотонный бюджет на весь вызов на всех путях, без сброса на батч; `TimeoutSeconds`/`Timeout(int)` удалены без shims; `≤0` — ошибка аргумента, дедлайн — `TimeoutException`, отмена пользователя побеждает; прежнее ограничение «timeout не поддерживается в PostgreSQL» снято |
| 7 | ClickHouse `MaxDegreeOfParallelism` / `WithoutSession` | **implemented** через `InsertBinaryAsync` как `ClickHouseBulkInsertOptions.MaxDegreeOfParallelism`/`UseSession`; `≥1` без сессии, `null` сохраняет настройку, `true` требует фактический session id; параллелизм > 1 с сессией и на портируемом пути — named refusal. `ClickHouseBulkCopy` 1.4.0 **не** obsolete (obsolete только `InitAsync`) |

**Documented no-effect** (не refusal и не поддержка): `KeepIdentity` без identity-колонки в маппинге — явные значения не-identity ключей пишутся, identity-форма не эмитится, путь не меняется; `TableLock=false`/`FireTriggers=false`/`CheckConstraints=true` — отсутствие запроса, поведение пути по умолчанию.

**Закрыто (issue #305, CHECK-находка):** MariaDB bulk `Returning*` — не плоский refusal, а **версионный гейт**: на явно настроенной версии сервера 13.0+ портируемый путь рендерит `INSERT ... RETURNING` и возвращает ключи, ниже (незаданная или более старая версия) — named refusal `NotSupportedException` с именем `Returning`. Гейт отдельный от диалектного (`SupportsReturning` остаётся `false`, поэтому одно-row insert/delete `RETURNING` по-прежнему отклоняются на любой версии). Live-сертификация идёт на `mariadb:11.4` и покрывает только ветку отказа; ветка 13.0+ сертифицирована офлайн по рендерированному SQL, live-оракул для неё появится вместе с сервером ≥ 13.

## 3. Проверено и закрыто (не переоткрывать)

Только тема + где смотреть; подробности — в документации и тикетах.

| Пункт linq2db | Как закрыто в nextorm | Документация |
|---|---|---|
| `#1994` value converters, кастомный маппинг | `ValueConverter<,>`/`[ValueConverter]`/`HasConversion` | [Value converters](../../infrastructure/03-value-converters.md) |
| `#1661` JSON-колонка ↔ CLR-объект | `[JsonColumn]`, native `json`/`jsonb` (#131), bare `JsonNode` (#197) | [JSON](../../guide/16-json.md#map-a-clr-object-to-a-native-json-column) |
| `#5717` композируемый DML `RETURNING` | PostgreSQL: `MutationCteQuery` (#136/#143); SQL Server — G22 | [Data-modifying CTE](../../guide/17-insert-statement.md#data-modifying-cte-postgresql) |
| `#3832`/`#2982` `OUTPUT INTO`, несколько result-set'ов | `OutputInto`/`OutputIntoThenOutput`, `BatchBuilder` + `BatchResult` (#70) | [SQL batch](../../guide/25-sql-batch.md) |
| `#2960`/`#5124`/`#3795` bulk-insert | returning/ignore/identity/chunking | [Bulk insert](../../guide/22-bulk-insert.md) |
| `#698` `Regex` в запросе | SQL Server 2025+ включительно, везде fail-closed | [Regular expressions](../../scalar-functions/01-string-functions.md#regular-expressions) |
| `#1645` table-valued parameters | нативно/JSON-эмуляция/`Array(T)` (#73) | [Raw SQL: procedures](../../guide/14-raw-sql.md) |
| `#5759`/`#4306`/`#2950` `[Duration]`/`TimeSpan` | native `interval`/`TIME`, целочисленная форма | [Duration columns](../../guide/24-duration-columns.md) |
| `#4562` PostgreSQL range/`Overlaps` | `Range<T>`, `[RangeColumns]` поверх пары скаляров | [Range columns](../../guide/27-range-columns.md) |
| `#5921`/`#5927` C# string-семантика, ordinal | бинарная коллация; колонковая коллация (#28) | [Ordinal comparison](../../scalar-functions/01-string-functions.md#ordinal-comparison-and-collation) |
| `#5933`/`#5948`/`#5952` версионные диалекты | явная `Version` контекста (#141) | [PostgreSQL: server version](../../connecting-providers/postgres.md#server-version) |
| `#5965` sub-day операнд над date-only | `ISqlDialect.PromoteDateOperand` | [Duration columns](../../guide/24-duration-columns.md) |
| `#3408` SQLite `json_each`/`json_tree` | встроенные TVF (#82) | [SQLite TVF](../../guide/provider-specific-sql/sqlite.md#json_each--json_tree-table-functions) |
| `#3869` PostgreSQL `jsonpath` | `jsonb_path_*` + TVF | [PostgreSQL: json/jsonb](../../guide/provider-specific-sql/postgresql.md#json-and-jsonb) |
| `#5822` recursive CTE + вычисляемая проекция | не подтвердилось: union inline; остаток — G21 | [CTE](../../guide/10-cte.md) |
| `#5879` `IndexExpression` в дереве | captured `dict`/`list`/`arr`, интерфейсные типы | [Filtering](../../guide/03-filtering-where.md#captured-collection-lookup-dictcolumn) |
| `#3015` `UPDATE`/`DELETE` через CTE | `WITH ... UPDATE`/`DELETE`, хойстинг перед мутацией | [CTE](../../guide/10-cte.md) |
| `#3009`/`#4039` кэш и логирование параметров | `Clear()`/`UseQueryCache(false)`/`UseCacheSlidingExpiration`, интерцепторы | [Cache controls](../../engine/03-plan-cache-and-reuse.md#cache-controls) |
| `#4405` оптимистичная конкурентность | by design, паттерн в гайде | [Optimistic concurrency](../../guide/26-optimistic-concurrency.md) |
| `#4543` query filters | глобальные фильтры | [Query filters](../../advanced/query-filters.md) |
| `#4044`/`#4611`/`#4666` EF Core | пакет `nextorm.entityframeworkcore` | [EF Core integration](../../advanced/integration-efcore.md) |
| `TagQuery`/`JoinHint`/`SubQueryHint`/`TablesInScopeHint` | `WithTag`, `WithJoinHint`, `WithSubQueryHint`, `WithTablesInScopeHint` (#96) | [Query hints](../../guide/15-query-hints.md) |
| runtime-override таблицы/схемы/сервера | `WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression` | [Per-query source overrides](../../guide/01-query-sources.md#per-query-source-overrides) |
| dynamic columns | read + write (#104) | [Dynamic columns](../../guide/29-dynamic-columns.md) |
| архитектурные эпики `7.0.0` (`#5675`/`#5718`/`#5758`/`#5812`) | ориентир для `DataContextCache`/метаданных, не gap | — |

## See also

- [Limitations и out-of-scope](../../advanced/limitations.md) — границы продукта и их причины.
- [`comparisons/capabilities.md`](../../comparisons/capabilities.md) — паритет по готовому функционалу.
- [`capability-matrix.md`](capability-matrix.md) — построчная матрица nextorm/EF/linq2db.
- [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) — пробелы по SQL-конструкциям.
