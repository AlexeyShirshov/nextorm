# TODO: table-valued parameters (TVP)
> Tracking issue: [#73](https://github.com/AlexeyShirshov/nextorm/issues/73).

> **Пересмотр 2026-09-26.** TVP строится на **фазе 0** `todo_stored_procedures.md`
> (дескриптор параметра с `TypeName`/`Structured`; core-поверхность не видит провайдерских типов).
> Порядок: после фазы 0 #70. Устаревшие ссылки на несуществующие `ctx.Parameter<T>` и одиночный
> `ctx.ExecuteRawAsync` заменены API #70. Блокер ревью о провайдерском типе в core-перегрузке снят.
> **Готово (2026-09-26):** фаза 0 #70 реализована — дескриптор `ProcedureParameter` с `TypeName` и SQL
> Server-хук `CreateProcedureParameter` (выставляет `SqlDbType.Structured`) на месте; `CommandReaderOwner`
> доступен для владения reader'ом.

> **Статус реализации (2026-09-26).** Фазы 1–3 закрыты: ядро — `ProcedureParameter.Table<T>` (две фабрики, без optional-`null`) + `TableParameterValue` (`TableValuedParameter<T>` — `internal`), capability `ISqlDialect.SupportsTableValuedParameters`; SQL Server — нативный `SqlDbType.Structured` + `TypeName`, поток `IEnumerable<SqlDataRecord>` (один переиспользуемый record на перечисление), пустой набор → null (SqlClient отклоняет `DBNull` для structured-параметра); PostgreSQL/SQLite/MySQL/MariaDB — JSON/массив-эмуляция через общий `TableParameterBinder`; ClickHouse — **нативная array-эмуляция** (см. ниже); in-memory — `NotSupportedException`. Тесты: core (носитель/биндер), dialect-unit (SQL Server/PostgreSQL/MySQL/MariaDB/SQLite/ClickHouse), ClickHouse-типы (enum, `TimeOnly` как `HH:mm:ss.fffffff`, `TimeSpan` в тиках, `byte[]` как `String`, nullable и пустой набор) и интеграционные SQL Server/PostgreSQL/MySQL/ClickHouse. Доки EN+RU и регистры обновлены (ClickHouse-форма описана в `docs/guide/14-raw-sql.md` и `docs/providers/clickhouse.md`; ClickHouse больше не числится неподдержанным). **Issue [#73](https://github.com/AlexeyShirshov/nextorm/issues/73) остаётся открытым**: decimal precision/scale реализован в цикле **tvp-2** (2026-09-26) — `IPropertyMetadata.DecimalPrecision`/`DecimalScale` + атрибут `[DecimalPrecision(p, s)]`/fluent, проброс через `TableParameterColumn` в `SqlMetaData` SQL Server и `Decimal(p, s)` ClickHouse, общая валидация `1..38`/`0 <= scale <= precision`; ожидает CHECK (unit-тесты зелёные, integration с `DOCKER_HOST` — в CHECK).
>
> **ClickHouse: array-based форма (2026-09-26, live spike `/tmp/tvp-array-probe.log`).** Вместо
> `format(JSONEachRow, structure, @p)` выбран прямой bound-массив: scalar-набор → `Array(T)`, entity-набор →
> `Array(Tuple(col1, col2, …))`, разворачивается серверно через `arrayJoin(@p)` (entity: `select t.1, t.2
> from (select arrayJoin(@rows) as t)`). Драйверу выставляется явный `ClickHouseType` (инференс CLR-значения
> делает элементы non-nullable и ломается на null-элементе/пустом массиве); nullable-колонка получает
> `Nullable(...)` внутри tuple, тип колонки берётся из `TableParameterColumn.ClrType`. Spike подтвердил:
> native-typed, `@`-inference и override — все три дают одинаковый результат для `Array(Int32)` и
> `Array(Tuple(...))`, включая пустые массивы и `arrayJoin`; N3/N4/N5 подтверждают путь
> `ExecuteRaw` + `ProcedureParameter`. `SupportsTableValuedParameters = true`.
>
> **Журнал решений.** Носитель — `public abstract TableParameterValue` (`Type RowType`, `IEnumerable<object> Rows`) + `internal TableValuedParameter<T>`: провайдерские типы в public API не попадают, конкретный тип скрыт. `TypeName` остаётся только на `ProcedureParameter` и не дублируется в носителе. Колонки сущности — все невычисляемые mapped-свойства в порядке метаданных, **включая identity**; `Range<T>` (два столбца) → `NotSupportedException`. `TypeName` на PostgreSQL/MySQL/SQLite → `ArgumentException` («TypeName is SQL Server only»): именованного типа у этих провайдеров нет, а тихое игнорирование недопустимо. Общий helper — `internal TableParameterBinder`; провайдеры используют публичные члены носителя `TableParameterValue` (`GetColumns`/`WriteJson`/`ToArray`), `protected`-хелперы `DataContext` не вводились. SQL Server type mapping: `string`→`nvarchar(max)`, `decimal`→`decimal(38,18)`, `TimeSpan`→`bigint` (единица из `DurationStorage`, по умолчанию тики), `byte[]`→`varbinary(max)`; пустая последовательность → null (SqlClient отклоняет `DBNull`: «Table-valued parameters cannot be DBNull»). Открытые вопросы 2–4 закрыты; вопрос 5 — TVP остаётся табличным параметром, `@in` — скалярным набором. **ClickHouse (2026-09-26):** формат — не `format(JSONEachRow, …)`, а bound-массив (`Array(T)`/`Array(Tuple(...))`) + `arrayJoin(@p)`; драйверу задаётся явный `ClickHouseType`, tuple-колонки нормализуются `TableParameterBinder.GetColumns` (пустой набор → пустой массив). `TableParameterValue.ToArray` для **non-nullable** value-type элемента даёт non-nullable массив (`int[]`, не `int?[]`), для nullable-элемента — `Nullable<T>[]`: это контракт метода, и от него зависит `Array(Int32)` на ClickHouse (иначе драйвер видит `Array(Nullable(Int32))`); nullable-элементы нужны только чтобы NULL-строки выживали.
>
> Рабочий план (design RFC). Источник — **G8** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md):
> `linq2db#1645`. Тесно связано с `todo_stored_procedures.md` (TVP чаще
> всего передаётся в процедуру) и с динамической схемой результата (серверные `values()`, см.
> [Dynamic result schema](../../guide/11-table-valued-functions.md#dynamic-result-schema)).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

> **Перф-замер decimal precision/scale — запись внесена 2026-09-26, *после* того как фаза 2
> (цикл `tvp-2`) была взята в работу (CHECK идёт в параллельной сессии).** Правка пришла
> мид-цикл, поэтому её не видит та CHECK-сессия; сознательно **не писали в замороженный
> статус-файл** `docs/specs/status/tvp-2.md` (он не обновляется в DO/CHECK). **Последующие
> фазы TVP и закрытие фичи #73 обязаны это учесть.**
> - Замер **не требуется** — изменение per-column/one-time, не на per-row пути:
>   `IPropertyMetadata.DecimalPrecision`/`DecimalScale` читаются один раз на параметр в
>   `TableParameterBinder.BuildColumns`
>   (`src/nextorm.core/DataContext/TableParameterBinder.cs:43-44`); вызов — SQL Server
>   `SqlServerDataContext.cs:156`, ClickHouse `ClickHouseDataContext.cs:106`, JSON-путь
>   `TableParameterBinder.cs:177`; валидация диапазона — на этапе конфигурации метаданных
>   (`EntityMetadataBuilder.cs:88`, `DecimalPrecisionRules.Validate`), метаданные кэшируются.
>   Per-row циклы (`Project` `SqlServerDataContext.cs:219-220`, `BuildRows`
>   `ClickHouseDataContext.cs:170-177`, `WriteJson` `TableParameterBinder.cs:179-191`,
>   `ToTypedArray` `TableParameterBinder.cs:230-239`) precision/scale не используют.
> - Горячий per-row путь TVP-биндинга **в acceptance-набор не входит** (набор — query-path:
>   in-memory `Count`/`GroupBy`, sqlite cached `Any`/`Where`/plan). **Deferred + триггер:** TVP
>   в горячем сценарии (≥10⁴ строк) → binder-level кейс в `benchmarks/nextorm.benchmark`
>   (`GetColumns` + per-row проекция/`WriteJson`/`ToArray`, без контейнера) и подключение к приёмке.
> - Правило «перф-замер — обязательное решение PLAN» закреплено в `.opencode/nextorm-pdca.md`
>   §«Перф-приёмка cached path».

## 1. Пункт и цель

- **Фича:** передать таблицу строк **параметром** (не источником `FROM`): SQL Server — нативный
  TVP (`SqlDbType.Structured` + `TypeName`), остальные — документированная эмуляция.
- **Критерий приёмки:**
  - **SQL Server:** `INSERT ... SELECT ... FROM @tvp`, вызов процедуры с TVP и их **async**-двойники
    выполняются на реальном контейнере (`ExecuteRaw`/`ExecuteRawAsync`, `ExecuteProcedure`/`ExecuteProcedureAsync`).
  - **SQLite:** эмуляция `json_each`/`json_extract` проверяется **интеграционно** (не только unit) на
    `SqliteTestProvider`.
  - **ClickHouse:** попытка `format(JSONEachRow, structure, bound parameter)` через **сырую** команду —
    только параметр-строка (не TVP-носитель, чтобы обойти гейт) — для **scalar/entity/empty**; тест на
    контейнере доказывает или опровергает пригодность формы.
  - **Fallback (отказ от ClickHouse-формы) — out of scope, пока не доказана реальная несовместимость**
    сервера/драйвера; «не работает» без падающего контейнерного теста не принимается.
  - **In-memory:** raw-команды не поддержаны, TVP через `ExecuteRaw` → `NotSupportedException`
    (проверяется unit-тестом); поддержка — только по CLR-коллекции в LINQ-пути.
  - **`@in` (Q5):** TVP — табличный параметр, `@in` — скалярный набор; API не дублируются, граница
    фиксируется этим планом.
  - **decimal precision/scale:** реализовано (цикл tvp-2, 2026-09-26) через `IPropertyMetadata.DecimalPrecision`/`DecimalScale` + `[DecimalPrecision]`/fluent; ожидает CHECK. Перф-замер **не требуется** (изменение per-column, не per-row — см. блок «Перф-замер» выше); учесть при закрытии #73.
  - Неподдержанная комбинация отклоняется понятным `NotSupportedException`.
- **Не про TVF-источники:** `[SqlTableFunction]`/`FromTableFunction` уже умеют таблицу как **источник**
  запроса; здесь — параметр.

## 2. Почему это нужно

1. **Батч-операции без N round-trip:** передать 1000 строк одним параметром на SQL Server вместо
   1000 `INSERT`; linq2db закрывает это в `#1645`.
2. **Вызов хранимых процедур с табличным аргументом** — основной сценарий TVP; без SP-поверхности
   (см. `todo_stored_procedures.md`) ценность ограничена.
3. nextorm уже умеет TVF как источник и `IN`-по-набору скаляров, но не «таблица как параметр».

## 3. Текущее состояние (проверено по коду)

- Ни `SqlDbType.Structured`, ни `DataTable`/`SqlDataRecord`, ни `IEnumerable<T>`-параметров в `src/`
  нет; `CommandType` не выставляется (всё — текстовый SQL), см. `todo_stored_procedures.md`.
- TVF-источник есть: `[SqlTableFunction]` + `FromTableFunction` (`SqlFunctions.Postgres.cs:578-592`
  `regexp_matches`, `SqlFunctions.cs` row-интерфейсы).
- Скалярный набор для `IN`: `CommonFunctions.@in<T>(T, IEnumerable<T>/params T[])`
  (`Query/SqlFunctions.cs:452-458`).

## 4. Матрица провайдеров

Источники: MS Learn «Table-valued parameters» (`SqlDbType.Structured`, `TypeName`, `DataTable`/
`DbDataReader`/`IEnumerable<SqlDataRecord>`, input-only); PostgreSQL 18 §9.19/§8.15 (arrays,
`unnest`, `jsonb_to_recordset`); MySQL 8.0 JSON (`JSON_TABLE`); MariaDB `JSON_TABLE` (10.6+); SQLite
JSON1 (`json_each`); ClickHouse `values()`/`input()`.

| Провайдер | native TVP | Форма | Источник |
|---|---|---|---|
| SQL Server | **да** | `SqlParameter { SqlDbType = Structured, TypeName = "dbo.T" }`, значение `DataTable`/`DbDataReader`/`IEnumerable<SqlDataRecord>`; input-only, нужен user-defined table type | MS Learn |
| PostgreSQL | — | эмуляция: массив + `unnest(arr)` / `= ANY(arr)`, либо `jsonb_to_recordset($1)` | PG 18 |
| MySQL | — | эмуляция: `JSON_TABLE` (8.0) / derived table (`UNION ALL`) | MySQL ref |
| MariaDB | — | эмуляция: `JSON_TABLE` (10.6+) / derived table | MariaDB KB |
| ClickHouse | — | **реализовано:** bound-массив `Array(T)` (scalar) / `Array(Tuple(...))` (entity) + `arrayJoin(@p)`; `values()`/`input()` отклонены (несовместимы с bound-параметром) | ClickHouse, spike `/tmp/tvp-array-probe.log` |
| SQLite | — | эмуляция: `json_each` (JSON1) / temp table | sqlite.org |
| InMemory | — | CLR-коллекция напрямую | — |

**Единообразие:** нативная поддержка — **только SQL Server** (provider-surface); PG/MySQL/MariaDB/
SQLite/ClickHouse получают эмуляцию, реализованную в фазе 3. Гейт `SupportsTableValuedParameters`
(default `false`, `true` у SQL Server/PG/MySQL/MariaDB/SQLite/ClickHouse; `false` у in-memory).

## 5. Ближайший CLR-аналог и тир

- Аналог — `IEnumerable<T>`/`IList<T>` как параметр, сопоставленный пользовательскому табличному типу.
- Тир **(b)**: provider-surface на SQL Server (`SqlServerDataContext` + новый публичный тип для
  табличного параметра); `[SqlFunction]` не подходит (нужен `SqlDbType`/`TypeName`, а не name-swap).

## 6. Дизайн и публичный API

```csharp
// SQL Server provider
public sealed class TableValuedParameter<T>
{
    public TableValuedParameter(string sqlTypeName, IEnumerable<T> rows);
}
```

- Structured/`TypeName` идёт через **дескриптор параметра фазы 0 #70**
  (`todo_stored_procedures.md`, §7 Фаза 0): core-перегрузки не видят
  `SqlDbType`/`SqlDataRecord`/`DataTable`. Провайдерский хук рядом с `CreateParam`
  (`SqlServerDataContext.CreateParam`, `src/nextorm.sqlserver/SqlServerDataContext.cs:59-64`) собирает
  `SqlParameter { SqlDbType = Structured, TypeName = ... }` из дескриптора.
- Точки входа (SQL Server): `ctx.ExecuteRaw`/`ExecuteRawAsync` и
  `ctx.ExecuteProcedure`/`ExecuteProcedureAsync` (#70, §7 Фаза 0) с `TableValuedParameter<T>` в единой
  форме параметров; для потока — `IEnumerable<SqlDataRecord>`/`DbDataReader` внутри провайдерской
  реализации.
- Эмуляция (PG): `ctx.ExecuteRaw`/`ExecuteRawAsync` (#70) с массивом/json-параметром +
  `FromTableFunction`/`jsonb_to_recordset`; либо helper `Unnest(rows, columnDef)`.
- In-memory: коллекция напрямую.
- Пока нет SP-поверхности, единственный сценарий — `INSERT ... SELECT ... FROM @tvp` через сырой SQL
  (`WithSql`); поэтому §7 фаза 1 имеет смысл только после **фазы 0** `todo_stored_procedures.md` (сырые
  команды #70 дают точку входа).

## 7. Этапы внедрения

Порядок: после **фазы 0** `todo_stored_procedures.md`; фаза 1 TVP может идти на сырых командах #70
(`ExecuteRaw`/`ExecuteRawAsync`) и не ждёт полной SP-поверхности.

1. **SQL Server TVP**: тип `TableValuedParameter<T>`, `Structured`/`TypeName` через дескриптор фазы 0; — реализовано (2026-09-26).
   SQL-gen + интеграция (container) на `INSERT ... SELECT FROM @tvp` через `ExecuteRaw`.
2. **Связка со SP**: передача TVP в `ExecuteProcedure`/`ExecuteProcedureAsync` (см. — реализовано (2026-09-26).
   `todo_stored_procedures.md`).
3. **Эмуляции**: PG `unnest`/`jsonb_to_recordset`, MySQL/MariaDB `JSON_TABLE`, SQLite `json_each`, — PG/MySQL/MariaDB/SQLite реализовано (2026-09-26);
   ClickHouse — **реализовано (2026-09-26)**: bound-массив `Array(T)`/`Array(Tuple(...))` + `arrayJoin(@p)`,
   а не `format(JSONEachRow, …)`. Живой сервер подтвердил пригодность формы (spike `/tmp/tvp-array-probe.log`,
   N1–N6); `SupportsTableValuedParameters = true`. Fallback не понадобился.

## 8. План тестов

**Failure-first (итерация 1, до правок ClickHouse):** сначала падающие целевые тесты, затем реализация.
SQL Server / SQLite — падают, если поведение ещё не реализовано; ClickHouse — тест доказывает/опровергает
форму (если форма работает, тест зелёный по факту; если нет — красный и фиксирует несовместимость).

- SQL-gen (`tests/nextorm.sqlserver.tests`): параметр `Structured` + `TypeName` в тексте команды. — есть.
- Интеграция (`tests/nextorm.integration.tests/SqlServerSpecificTests.cs`):
  - создать user-defined table type;
  - `INSERT ... SELECT ... FROM @tvp` (сырой SQL) + async-двойник;
  - вызов процедуры с TVP + async-двойник;
  - пустой набор; null-колонки. — частично есть (sync raw/proc/empty/null), добавляются INSERT SELECT и async.
- SQLite-интеграция: `json_each`/`json_extract` round-trip на `SqliteTestProvider` (scalar/entity/empty). —
  добавляется (сейчас только unit в `tests/nextorm.sqlite.tests`).
- ClickHouse (контейнер, `ClickHouseTableValuedParameterTests`): `select arrayJoin(@p)` для scalar и
  `select t.1, t.2 from (select arrayJoin(@p) as t)` для entity; scalar/entity/empty + async + отклонение
  `TypeName`. Тест гоняется на реальном сервере через тот же TVP-носитель (не сырую строку), поэтому
  подтверждает и binding драйвера, и серверное разворачивание.
- ClickHouse unit (`tests/nextorm.clickhouse.tests/TableValuedParameterTests.cs`): форма `ClickHouseType`
  (`Array(Int32)`, `Array(Nullable(Int32))`, `Array(Tuple(Int32, Nullable(String)))`), пустой массив и
  nullable в элементах. Ожидание `Array(Int32)` для non-nullable `int[]` требует, чтобы `ToArray` не
  раздувал non-nullable element type до `Nullable<int>` (см. журнал решений).
- PG-эмуляция: `unnest`/`jsonb_to_recordset` round-trip (`PostgresSpecificTests.cs`). — есть.
- In-memory: raw `ExecuteRaw` с TVP → `NotSupportedException` (unit). — есть (`RawCommandInMemoryTests`).
- decimal: precision/scale из `IPropertyMetadata` — реализовано (цикл tvp-2, 2026-09-26): unit-тесты атрибута/fluent/диапазонов и проброса в `TableParameterColumn` (SQL Server/ClickHouse); integration-кейсы прогоняются в CHECK.
- Покрытие (2026-09-26): line **86.2 %** / branch **76.5 %** — не ниже pre-change baseline (line 86.2 %);
  CI-порог `MIN_LINE_COVERAGE=75` не опущен. SQL Server входит в `coverage.settings.xml`; PG-эмуляция тоже;
  baseline снимается `dotnet-coverage` + `reportgenerator` по методологии CI (`/tmp/tvp-coverage-before.log`).

## 9. Открытые вопросы

1. ~~Делать ли TVP сразу или после `todo_stored_procedures.md` (основной сценарий — SP).~~
   **Решено (2026-09-26):** после **фазы 0** #70; фаза 1 TVP может идти на сырых командах #70.
2. API: `TableValuedParameter<T>` (provider-specific) vs cross-provider `SetParameter<T>`.
   **Решено (2026-09-26):** `TableParameterValue` — provider-нейтральный носитель в ядре,
   `TableValuedParameter<T>` internal; провайдерские типы в public API не попадают.
3. SQL Server: `IEnumerable<SqlDataRecord>` (стриминг) vs `DataTable` (проще, но требует
   `Microsoft.Data.SqlClient`-типов) — не тянуть ли провайдерские типы в публичный API.
   **Решено (2026-09-26):** фабрика `ProcedureParameter.Table<T>` стримит `IEnumerable<SqlDataRecord>`
   (один переиспользуемый record); устаревшие `DataTable`/`DbDataReader` через `TypeName` по-прежнему
   принимаются и не удаляются.
4. Нужны ли эмуляции на PG/MySQL/MariaDB, или ограничиться SQL Server + документировать.
   **Решено (2026-09-26):** эмуляции реализованы для PG/MySQL/MariaDB/SQLite; ClickHouse — bound-массив `Array(T)`/`Array(Tuple(...))` + `arrayJoin(@p)` (см. §7, §4).
5. ~~Связь с `@in`-набором: не дублировать ли (TVP шире, но `@in` уже есть).~~
   **Решено (2026-09-26):** не дублировать. Граница: **TVP — табличный параметр** (`FROM @tvp`,
   multi-column, передаётся в процедуру), **`@in` — скалярный набор** (`SqlFunctions.Common.@in<T>`)
   внутри LINQ-предиката. Оба остаются; `@in` не переиспользует TVP-носитель.

## 10. Файлы к изменению

- Новое: `src/nextorm.sqlserver/TableValuedParameter.cs` (или в core с provider-хуком).
- Правки: `src/nextorm.sqlserver/SqlServerDataContext.cs` (`CreateParam`/`Structured`),
  `DataContext/DataContext.cs` (создание команды/параметра), при эмуляциях — соответствующие диалекты
  и `SqlFunctions.*`.
- Доки: `docs/guide/14-raw-sql.md` (+RU) — секция табличных параметров, включая ClickHouse
  `Array(T)`/`Array(Tuple(...))` + `arrayJoin(@p)` и оговорки (нет `TypeName`, нет хранимых процедур,
  только поддерживаемые CLR-типы, не потоковый bulk); `docs/providers/clickhouse.md` (+RU) — провайдерная
  секция и таблица различий; `docs/providers/sqlserver.md` (+RU); `docs/advanced/api-reference.md` (+RU);
  `docs/advanced/limitations.md` (+RU); `docs/comparisons/capabilities.md` (+RU);
  `docs/specs/roadmap/todo_tvp.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
  `docs/specs/comparison/linq2db-backlog-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 2 блокера**.

- **[DIP]/[OCP] 🔴** §6 (`:74-76`) предлагает core-перегрузку `ctx.ExecuteRawAsync(…, TableValuedParameter<T>)`, но `TableValuedParameter<T>` — в `src/nextorm.sqlserver/` (`:111`), а core не может ссылаться на провайдерскую сборку. Fix: провайдерский extension в `nextorm.sqlserver` (не core-метод) либо core-интерфейс (2-го потребителя пока нет → extension, инвариант 1).
- **[TYPE]/[SRP] 🔴** `TableValuedParameter<T>(string sqlTypeName, IEnumerable<T> rows)` (`:68-71`) не описывает деривацию колоночной схемы из `T`; `SqlDbType.Structured` требует колонок, а образец строит их из `IPropertyMetadata` (`src/nextorm.sqlserver/SqlServerDataContext.cs:218-232`). Fix: добавить источник схемы в сигнатуру либо зафиксировать reflection-конвенцию.
- **[TYPE] 🟡** `IEnumerable<T> rows` (ленивый enumerable) без контракта владения/одноразовости → двойное перечисление/использование после dispose. Fix: `IReadOnlyList<T>`/`IAsyncEnumerable<T>` с явным контрактом.
- **[DRY] 🟡** §3/§6 ссылаются на `ctx.Parameter<T>("jsonb", rows)` (`:34,77`) — такого члена нет; существующий `SqlFunctions.Parameter<T>(int idx)` (`src/nextorm.core/Query/SqlFunctions.cs:72`) — placeholder внутри LINQ. Fix: убрать ссылку или явно пометить как новое API.
- **[DRY] ℹ️** Пересечение с `@in`-набором (`SqlFunctions.cs:473,479`). Deferred: зафиксировать разграничение (TVP — табличный параметр, `@in` — скалярный набор).
- **[OCP] 🟡** `SupportsTableValuedParameters` (`:55-56`) без носителя гейта. Fix: явный DIM на `ISqlDialect`/`DialectCapabilities`.
- **[DRY] 🟡** Устаревшие якоря: `SqlFunctions.Postgres.cs:578-592` → `:552,556`; `SqlFunctions.cs:452-458` → `:473,479`. Fix.
- **[PERF] ℹ️** `IEnumerable<T>` → per-row `SqlDataRecord`/`DataTable` (SQL Server умеет `DbDataReader`-streaming). Deferred (профиль переноса ≥ 10⁴ строк).

> Обновление 2026-09-26: блокер §6 (провайдерский тип в core-перегрузке) снят — TVP идёт через
> дескриптор фазы 0 #70, публичная core-поверхность провайдерских типов не видит; ссылки на
> `ctx.Parameter<T>` и одиночный `ctx.ExecuteRawAsync` убраны (API #70 `ExecuteRaw`/`ExecuteRawAsync`,
> `ExecuteProcedure`/`ExecuteProcedureAsync`).

> **Реализовано, ожидает CHECK (2026-09-26, цикл tvp-2).** TVP SQL Server и ClickHouse читают
> precision/scale из `IPropertyMetadata` (`DecimalPrecision`/`DecimalScale`, атрибут `[DecimalPrecision(p, s)]`
> или fluent `EntityPropertyBuilder<T>.DecimalPrecision(p, s)`), проброшенные через `TableParameterColumn`;
> SQL Server строит `SqlMetaData` типа `decimal(p, s)`, ClickHouse — `Decimal(p, s)`. Валидация общая:
> precision `1..38`, `0 <= scale <= precision`, объявление на не-`decimal` свойстве — `InvalidOperationException`.
> Дефолты сохранены (SQL Server `decimal(38,18)`, ClickHouse `Decimal(38, 10)`, `ulong` `decimal(20,0)`);
> provider DDL/cast-дефолты не менялись. Unit-тесты зелёные; integration с `DOCKER_HOST` и закрытие issue
> [#73](https://github.com/AlexeyShirshov/nextorm/issues/73) — в CHECK/ACT. Остальные провайдеры
> (JSON/array-эмуляция) precision/scale JSON-представления не требуют.

> **Deferred (2026-09-26, corrective 3/3).** TVP **не поддерживает** `ValueConverter<*, TimeSpan>` на
> провайдерах без нативного типа длительности (SQL Server, SQLite, ClickHouse): столбец отображаемой
> сущности отклоняется понятным `NotSupportedException` при построении столбцов, до потребления строк.
> Общий шов записи `DurationStorage.ToParameterValue` при этом контракт конвертера не меняет — возвращает
> сконвертированный `TimeSpan`; на нативных провайдерах (PostgreSQL `interval`, MySQL/MariaDB `TIME`)
> TVP сохраняет `ProviderType`/значение `TimeSpan`. Триггер: у провайдеров появится нативное
> metadata/связывание длительности для конвертерного target (`TimeSpan` provider type) — тогда
> ограничение снимается. Регрессия общего пути закрыта `tests/nextorm.sqlite.tests/DurationConverterTests.cs`.
