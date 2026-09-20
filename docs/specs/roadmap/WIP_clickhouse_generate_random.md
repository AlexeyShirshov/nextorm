# WIP: ClickHouse — табличная функция `generateRandom`

- Пункт бэклога: `docs/specs/roadmap/todo_clickhouse.md:345-350`, `docs/specs/roadmap/todo_phase2.md:40`.
- Целевой провайдер: ClickHouse.
- Критерий приёмки: `SqlFunctions.ClickHouse.generate_random()` (и overload с seed) доступен через
  `FromTableFunction`, рендерит `generateRandom('<fixed structure>')` через `WrapTableFunction` с
  приведением `UInt64`→`Int64`, возвращает row-тип `SqlFunctions.IGenerateRandomRow`; остальные
  провайдеры отклоняют через `SupportsTableFunction("generateRandom")` + `NotSupportedException`.
  `url`/`s3`/`remote`/`file`/`cluster`/`values`/`system.one` остаются заблокированными.

## Матрица «провайдер × форма»

Источники: ClickHouse table-functions/index, `generateRandom` (clickhouse.com/docs/en/sql-reference/table-functions/generate);
PostgreSQL official reference (`generate_series`, `unnest`, `random`); Microsoft Learn
(`STRING_SPLIT`, `OPENJSON`, `RAND`); MySQL/MariaDB `JSON_TABLE` (MySQL) and no random-row TVF;
SQLite core functions.

| Провайдер | Механизм TVF | Аналог `generateRandom` |
|---|---|---|
| PostgreSQL | `generate_series`, `unnest` | — (нет генератора строк со схемой; `random()` — скаляр) |
| SQL Server | `string_split`, `openjson` | — (нет; `RAND()` — скаляр) |
| MySQL | `JSON_TABLE` | — (нет генератора строк) |
| MariaDB | — (нет встроенных TVF) | — |
| SQLite | — (нет встроенных TVF) | — |
| ClickHouse | `numbers`, `numbers_mt`, `zeros`, `zeros_mt`, `generateRandom`, `values`, … | `generateRandom('name Type, …'[, seed[, max_string_length[, max_array_length]]])` |
| InMemory | — (бросает `NotSupportedException`) | — |

Единообразие: `generateRandom` — ClickHouse-only. Схема в ClickHouse задаётся строкой (динамическая),
поэтому статический `IQueryable<T>` возможен только для фиксированной структуры. Библиотека
фиксирует структуру `'id UInt64, value Float64, name String'` и row-тип `IGenerateRandomRow`
(`Id long`, `Value double`, `Name string?`); произвольную схему пользователь по-прежнему может
объявить своей `[SqlTableFunction("generateRandom")]`-обёрткой.

## C#-аналог и tier

Прямого BCL-аналога нет. Tier (b): методы на `ClickHouseFunctions` с `[SqlTableFunction]` +
row-интерфейс в `SqlFunctions.cs` + гейт `SupportsTableFunction` + `WrapTableFunction`. Образец —
`numbers`/`zeros`.

## Семантика и обёртка

- `generateRandom()` — бесконечный поток случайных строк со случайной схемой; статической её сделать
  нельзя, поэтому диалект подставляет фиксированную структуру.
- `generateRandom('<structure>')` — строки с заданной схемой.
- `generateRandom('<structure>', seed)` — детерминированные значения при фиксированном seed.
- `WrapTableFunction("generateRandom", call)` формирует структуру и оборачивает вывод:
  `(select toInt64(id) as id, value, name from generateRandom('<structure>'[, seed]))` — `id` объявлен
  `UInt64`, поэтому приводится к `Int64` для row reader'а (как `numbers`).
- Результат бесконечен — обязателен `Page`/`First`.

## Диалектный план

- `ClickHouseDialect.SupportsTableFunction` → `true` для `"generateRandom"`.
- `ClickHouseDialect.WrapTableFunction` → ветка `generateRandom`.

## Публичный API

- `SqlFunctions.IGenerateRandomRow` (`[Column("id")] long Id`, `[Column("value")] double Value`,
  `[Column("name")] string? Name`).
- `ClickHouseFunctions.generate_random()` и `generate_random(long seed)` → `IQueryable<IGenerateRandomRow>`.

## План тестов

- CH SQL-gen: `TableFunction_GenerateRandom_ShouldEmitWrappedCall`,
  `TableFunction_GenerateRandomWithSeed_ShouldPassSeed`.
- Хуки: `SupportsTableFunction("generateRandom")` → `true`.
- Rejection PG: `BuiltInTableFunction_GenerateRandom_UnsupportedByProvider_ShouldThrow`.
- Интеграция CH: `GenerateRandomTableFunction_ShouldReturnRequestedRows` (реальный ClickHouse).

## Покрытие

`coverage.settings.xml` не включает `nextorm.clickhouse`; число не сдвинется, добавляются SQL-gen/dialect
тесты.

## Документация

`docs/guide/13-table-valued-functions.md` (+RU), `docs/providers/clickhouse.md` (+RU),
`docs/providers/overview.md` (+RU), `docs/specs/roadmap/todo_clickhouse.md`,
`docs/specs/roadmap/todo_phase2.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
