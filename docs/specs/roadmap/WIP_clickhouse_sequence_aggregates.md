# WIP: ClickHouse «продвинутые» агрегаты `windowFunnel`/`retention`/`sequenceMatch`

- Пункт бэклога: `docs/specs/roadmap/todo_phase2.md:39`; `docs/specs/roadmap/todo_clickhouse.md:178-179`.
- Целевой провайдер: ClickHouse (модель запроса не меняется).
- Критерий приёмки: `SqlFunctions.ClickHouse` предоставляет `window_funnel`, `sequence_match`,
  `retention`; вызовы рендерятся в нативные CH-имена с корректной типизацией (DateTime/условия) и
  приведением результата (`toInt32(...)` для `UInt8`/`Integer`), чтобы row reader материализовал
  скаляр; `retention` возвращает массив, поэтому применим только вложенно (row reader `Array(T)`
  отсутствует). Прочие провайдеры отвергают вызов `NotSupportedException`.

## Провайдер × форма (шаг 1)

Источники: ClickHouse — официальный справочник aggregate functions → parametric
(clickhouse.com/docs/en/sql-reference/aggregate-functions/parametric-functions: `windowFunnel`,
`retention`, `sequenceMatch`/`sequenceCount`); PostgreSQL — агрегатные функции
(postgresql.org/docs/current/functions-aggregate.html); SQL Server / MySQL / MariaDB / SQLite —
соответствующие справочники (аналогов нет); InMemory — поверхность CH-only.

| Провайдер | `windowFunnel` | `retention` | `sequenceMatch` | Источник |
|---|---|---|---|---|
| PostgreSQL | `—`: нет агрегата воронки (эмулируется оконными/`FILTER` вручную) | `—`: нет агрегата-маски (эмуляция через `bool_or`-набор) | `—`: нет | functions-aggregate.html |
| SQL Server | `—` | `—` | `—` | learn.microsoft.com/sql/t-sql/functions/aggregate-functions-transact-sql |
| MySQL | `—` | `—` | `—` | dev.mysql.com/doc/refman/8.4/en/aggregate-functions.html |
| MariaDB | `—` | `—` | `—` | mariadb.com/kb/en/aggregate-functions/ |
| ClickHouse | `windowFunnel(window[, modes])(timestamp, cond1..condN)` → Integer | `retention(cond1..cond32)` → `Array(UInt8)` | `sequenceMatch(pattern)(timestamp, cond1..condN)` → `UInt8` | clickhouse.com/docs/en/sql-reference/aggregate-functions/parametric-functions |
| SQLite | `—` | `—` | `—` | sqlite.org/lang_aggfunc.html |
| InMemory | `—`: CH-only; in-memory не участвует | — | — | nextorm (гейт `SupportsSequenceAggregates`) |

**Единообразие провайдеров.** Аналогов воронки/удержания/последовательности нет ни у одного
провайдера, кроме ClickHouse (это агрегаты с собственным синтаксисом «параметр(ы) в первых скобках,
аргументы во вторых» и несовместимой типизацией). Поэтому — **ClickHouse-поверхность**
`ClickHouseFunctions` (tier b) под новым флагом `ISqlDialect.SupportsSequenceAggregates` (default
`false`; ClickHouse `true`) и хуком `MakeSequenceAggregate`. Отдельный флаг под каждый агрегат не
нужен: все три — одно семейство, и провайдер без флага бросает `NotSupportedException` с понятным
сообщением. PG-эмуляции (`bool_or`, оконные) не идентичны по семантике и в следующих проходах не
промоутятся.

Имена CH = camelCase от CLR-имени: `window_funnel`→`windowFunnel`, `sequence_match`→`sequenceMatch`,
`retention`→`retention`.

## Ближайший аналог C# и tier

- BCL-члена нет. Tier (b): методы `ClickHouseFunctions` + ветка
  `Visitors/AdvancedAggregateTranslator.cs` + новый хук `MakeSequenceAggregate`.
- Семейство параметрических агрегатов по форме совпадает с `quantile(level)(value)` (двойные
  скобки), но у `windowFunnel` параметр — окно, у `sequenceMatch` — паттерн, у `retention` скобки
  одинарные; обобщённый `MakeQuantile` не подходит.

## Диалектный план

- `ISqlDialect.SupportsSequenceAggregates` (default `false` в `SqlDialectBase`; ClickHouse `true`).
- `ISqlDialect.MakeSequenceAggregate(string name, string? parameters, string arguments)` — default
  бросает `NotSupportedException`; ClickHouse маппит snake_case → camelCase, рендерит
  `name(parameters)(arguments)` при непустых `parameters` и `name(arguments)` иначе; для
  `window_funnel`/`sequence_match` оборачивает результат в `toInt32(...)` (нативные `Integer`/`UInt8`
  не читаются row reader'ом как CLR `int`), для `retention` каст не делает (возвращает массив).
- `SqlDialectBase` — `SupportsSequenceAggregates => false`, `MakeSequenceAggregate` бросает.
- Прочие диалекты — без изменений.

## Публичный API (точные сигнатуры)

`ClickHouseFunctions`:
- `int window_funnel<TTime>(long window, TTime? timestamp, params bool[] conditions)`;
- `int sequence_match<TTime>(string? pattern, TTime? timestamp, params bool[] conditions)`;
- `int[] retention(params bool[] conditions)`.

Каждый — `<summary>` с оговоркой о провайдере/вложенности. Условия передаются **inline**
(`params bool[]`); захваченный массив условий не поддерживается (бросается `NotSupportedException`),
т.к. его элементы невыразимы как SQL-предикаты. Регистр — `API-NAMING-REVIEW.md`.

## План тестов

- CH SQL-gen (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`): `WindowFunnel_*`,
  `SequenceMatch_*`, `Retention_*` (имена, двойные скобки, `toInt32`, массив вложенно).
- CH хук (`ClickHouseDialectTests.cs`): `MakeSequenceAggregate_ShouldMapProviderNames`.
- Rejection (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`):
  `ClickHouseSequenceAggregates_UnsupportedByProvider_ShouldThrow`.
- Интеграция (`ClickHouseIntegrationTests.cs`, реальный ClickHouse):
  `WindowFunnel_ShouldCountConsecutiveConditions`, `SequenceMatch_ShouldMatchPattern`,
  `Retention_ShouldReturnConditionMask` (на `simple_entity`, ids 1..10 как монотонный timestamp).
- In-memory: поверхность ClickHouse-only, in-memory не участвует.
- Покрытие: `coverage.settings.xml` не включает `nextorm.clickhouse`, число не изменится; SQL-gen
  тесты обязательны.

## Документация

`docs/guide/04-grouping-and-aggregates.md` (+RU), `docs/providers/clickhouse.md` (+RU),
`docs/guide/provider-specific/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/roadmap/todo_clickhouse.md`,
`docs/specs/roadmap/todo_phase2.md`, `docs/specs/design/API-NAMING-REVIEW.md`.

## Статус

Реализовано, протестировано; см. коммит `CH sequence aggregates`.
