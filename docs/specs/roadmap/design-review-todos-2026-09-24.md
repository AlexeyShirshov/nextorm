# Дизайн-ревью всех открытых TODO (nextorm-design-engineer)

> **Внутренний артефакт:** этот файл не публикуется и не линкуется из `docs/**`/`readme.md` (AGENTS.md).
> Детальные находки (все severity) живут в разделе `## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)`
> каждого `docs/specs/roadmap/todo_*.md`; здесь — сводка, сквозные темы и явный список блокеров.

## 1. Метод и охват

- **Дата:** 24.09.2026. **Ветка:** `1.0.6-alpha`, dirty worktree (правится владельцем параллельно).
- **Инструмент:** сабагент `nextorm-design-engineer` (скиллы `dotnet-solid-principles`,
  `type-design-performance`, `analyzing-dotnet-performance`), запуск **read-only**: правок в код не
  вносилось, реестры (`solid-review.md`/`code-smells-review.md`/`API-NAMING-REVIEW.md`) не менялись.
- **Охват:** 27 планов `docs/specs/roadmap/todo_*.md`. Файл `todo_batch_dml.md` был удалён владельцем
  во время прогона (вместе с `todo_sql_batch.md`), поэтому фактически проверено **26** файлов.
- `file:line` — по дереву на момент ревью; перепроверять перед реализацией.
- Линзы находок: `[SRP] [OCP] [LSP] [ISP] [DIP] [DRY] [TYPE] [PERF]`; severity: 🔴 / 🟡 / ℹ️.
- **Недетерминированность:** результат сгенерирован ИИ; возможны ложные срабатывания и пропуски,
  замеров производительности не выполнялось.

## 2. Сквозные (системные) риски

1. **Name-based диспетчер трансляторов** — главный риск пяти планов. `BuiltinFunctionTranslator`
   (`BuiltinFunctionTranslator.cs:72`) и `ExtendedScalarFunctionTranslator` (`:34,50,65`) матчат по
   `Method.Name` без `DeclaringType` и вызываются раньше провайдерных (`NormSqlTranslator.cs:437,452`).
   Новые имена `format`/`md5`/`extract`/`regexp_*`/`to_*` либо молча уйдут в чужой рендер, либо бросят
   «require PostgreSQL». Затронуты: `todo_sqlserver_function_gaps`, `todo_mysql_function_gaps`,
   `todo_mariadb_function_gaps`, `todo_clickhouse_function_gaps`, `todo_sqlite_function_gaps`.
   Нужен DeclaringType-гейт/порядок (прецедент — `JsonSqlTranslator.cs:30-33`).
2. **Дублирование per-name capability-интерфейсов.** `ISqlServerFunctions`/`ISqliteFunctions`/
   `IMySqlFunctions`/`IClickHouse*Functions` повторяют отгруженный `IScalarFunctions`
   (`DialectCapabilities.cs:340`) — нарушение инварианта 1/KISS.
3. **`static` vs `instance` форма DSL.** Планы объявляют `public static class/methods`, тогда как
   отгруженная поверхность — вложенные **instance**-классы (`SqlFunctions.Postgres.cs:12` и т.п.);
   часть сигнатур не компилируется (`typeof`/`if` — ключевые слова; static-класс как тип).
4. **Дублирующиеся слои/seam'ы.** value-converters ↔ duration; `mapping_scope` не протягивает scope в
   `QueryPlanner._fromCache`/`SqlBuildContext`/`VisitorOptions`; `output_into` предлагает multi-result,
   хотя `BatchRunner.NextResult` уже есть.
5. **Владение/Dispose не задано.** `ProcedureResult` (не `IAsyncDisposable`), LOB-поток держит команду
   из план-кэша, JSON-стрим — общий `AfterCompletion`/наблюдаемость.
6. **Процессный план `todo_public_api_freeze`** конфликтует с `TreatWarningsAsErrors=true` и
   alpha-политикой «слом допустим»; генерация `Shipped` в один проход обходит курированный список.
7. **Устаревшие якоря `file:line`** почти во всех планах (дерево правится live). Отдельно:
   `API-NAMING-REVIEW.md:4653` (BAT6) и `code-smells-review.md:6740` ссылаются на уже удалённый
   `todo_batch_dml.md`.

## 3. Сводка по файлам

| # | Файл | Вердикт | Находок | Блокеров |
|---|---|---|---|---|
| 1 | `todo_sqlserver_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 2 | `todo_postgres_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 3 | `todo_sqlite_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 4 | `todo_cross_provider_scalar_functions.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 5 | `todo_mysql_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 6 | `todo_mariadb_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 7 | `todo_clickhouse_function_gaps.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 8 | `todo_clickhouse_aggregate_function_state.md` | не готов к ревью | 5 | 1 (полнота) |
| 9 | `todo_query_filters.md` | нужен пересмотр | 9 | 3 |
| 10 | `todo_batch_dml.md` | **файл удалён** | — | — |
| 11 | `todo_output_into.md` | пересмотрено 2026-09-26 | 7 | 2 |
| 12 | `todo_dynamic_result_schema.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 13 | `todo_stored_procedures.md` | пересмотрено 2026-09-26 | 9 | 1 |
| 14 | `todo_tvp.md` | пересмотрено 2026-09-26 | 8 | 2 |
| 15 | `todo_sharding.md` | нужен пересмотр | 10 | 1 |
| 16 | `todo_json_column_mapping.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 17 | `todo_interface_poco.md` | дизайн-здоров | 5 | 0 |
| 18 | `todo_mapping_scope.md` | нужен пересмотр | 12 | 1 |
| 19 | `todo_join_projection_mapping.md` | дизайн-здоров | 4 | 0 |
| 20 | `todo_timespan_columns.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 21 | `todo_value_converters.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 22 | `todo_streaming_lob.md` | **файл удалён (<span style="color:green">shipped</span>, #27, 2026-09-29)** | 9 | 1 |
| 23 | `todo_json_streaming.md` | нужен пересмотр | 9 | 1 (корректность) |
| 24 | `todo_efcore_integration.md` | **файл удалён (<span style="color:green">shipped</span>, #61, 2026-09-29)** | 8 | 1 (архитектура) |
| 25 | `todo_interceptors.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 26 | `todo_postgres_ranges.md` | **файл удалён (<span style="color:green">shipped</span>)** | — | — |
| 27 | `todo_public_api_freeze.md` | пересмотр шагов 2/4 | 10 | 2 |
| | **Итого** | | **~197** | **~30** |

## 4. Блокеры (🔴) — устранить до реализации

### Функции провайдеров

- `todo_clickhouse_aggregate_function_state.md`
  - `:45-49` публичный API не определён (нет сигнатур/capability/типа материализации/тест-плана).
### Запросы / DML / результат

- `todo_query_filters.md`
  - `:75-76` лямбда в атрибуте невыразима → `string?` имя;
  - `:90` новый член `IEntityMetadata` без DIM — source-breaking;
  - `:105-107` нет механизма «контекстное значение → runtime-параметр».
- `todo_output_into.md`
  - `:78` `Into()` возвращает returning-билдер без клиентского набора (`Single()` бросает);
  - `:89-98` multi-result дублирует существующий `BatchRunner.NextResult` (`BatchRunner.cs:199-219`).
- `todo_stored_procedures.md`
  - `:69-75` `ProcedureResult` не `IAsyncDisposable`, владение reader'ом не задано.
- `todo_tvp.md`
  - `:74-76,111` provider-тип в core-перегрузке (core не может ссылаться на провайдер);
  - `:68-71` нет деривации колоночной схемы из `T`.
- `todo_sharding.md`
  - `:141-143` `AcrossShards()` → единый `EntityBuilder<T>` нереализуем без scope-aware fan-out.

### Маппинг / стриминг / EF

- `todo_mapping_scope.md`
  - `QueryPlanner.cs:509-519` `_fromCache` process-wide не scope-aware.
- ~~`todo_streaming_lob.md`~~ — **файл удалён (<span style="color:green">shipped</span>, #27, 2026-09-29)**; отложенный хвост — `code-smells-review.md` §«Перенесено из status закрытого потока `lob-streaming`».
  - `:86-92` (историческое) терминал на `EntityBuilder<TResult>`, но `Select` возвращает `QueryCommand<TResult>` (`EntityBuilder.cs:170`).
- `todo_json_streaming.md`
  - `:96-104` JSON-writer обходит провайдерскую политику чтения (корректность).
- ~~`todo_efcore_integration.md`~~ — **файл удалён (<span style="color:green">shipped</span>, #61, 2026-09-29)**.
  - `:93` (историческое) противоречие авто-регистрации провайдеров (ссылки на провайдеры vs их отсутствие).

### PG ranges / заморозка API

- `todo_public_api_freeze.md`
  - `:18-20` hard-gate RS0016/17/25 против alpha-политики и `TreatWarningsAsErrors`;
  - `:38` генерация `Shipped` в один проход обходит курированный список.

## 5. Что дальше

- Полные находки, включая 🟡/ℹ️, с `file:line` и fix — в разделах каждого `todo_*.md`.
- Регистры находок (`solid-review.md`, `code-smells-review.md`, `API-NAMING-REVIEW.md`) не обновлялись;
  переносить туда — отдельным решением.
- Правило процесса: при создании/добавлении нового TODO запускать `nextorm-design-engineer` по плану.
- При починке реестровых ссылок: `API-NAMING-REVIEW.md:4653` (BAT6) и `code-smells-review.md:6740`
  указывают на удалённый `todo_batch_dml.md`.
- **Пересмотрено 26.09.2026:** планы `todo_output_into` (фаза 2), `todo_stored_procedures`, `todo_tvp`
  и `todo_streaming_lob` (позже удалён) унифицированы вокруг #70 «Фаза 0 — Основа» (дескриптор параметра, владелец
  reader'а с отдельной `DbCommand` на вызов, общий helper навигации result-set'ов, мапперы произвольного
  `T`); #25 (несколько result-set'ов) слит в #70 фазу 4.
