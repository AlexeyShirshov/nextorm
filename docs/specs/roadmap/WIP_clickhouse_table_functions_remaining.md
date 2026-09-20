# WIP: остальные табличные функции ClickHouse (`zeros` / `zeros_mt`)

> Рабочий план по скилу `implementing-todo-features`. Источник: `todo_clickhouse.md` →
> «Уровень 3 → Остальные табличные функции».

## Пункт и цель

- Фича: встроенные табличные функции ClickHouse, которых нет в `SqlFunctions`, кроме уже
  реализованных `numbers`/`numbers_mt`.
- Провайдер: ClickHouse (внешние и требующие конфигурации сервера функции — вне объёма).
- Критерий приёмки: `zeros`/`zeros_mt` доступны через `FromTableFunction`, рендерят
  `zeros(@count)`/`zeros_mt(@count)` и работают на реальном ClickHouse; остальные провайдеры
  отклоняют через `SupportsTableFunction` + `NotSupportedException`.

## Провайдер × форма (шаг 1)

Источник — официальная документация ClickHouse:
[`table-functions/zeros`](https://clickhouse.com/docs/en/sql-reference/table-functions/zeros),
[`system-tables/one`](https://clickhouse.com/docs/en/operations/system-tables/one),
[`table-functions/generate`](https://clickhouse.com/docs/en/sql-reference/table-functions/generate);
для остальных провайдеров — их справочники функций (PostgreSQL TVF `generate_series`/`unnest`,
SQL Server `string_split`/`openjson`; MySQL/MariaDB/SQLite встроенных TVF нет).

| Провайдер | Механизм TVF | Аналог `zeros` |
|---|---|---|
| PostgreSQL | `generate_series`, `unnest` | — (нет; строки генерирует `generate_series`) |
| SQL Server | `string_split`, `openjson` | — (нет) |
| MySQL | — (нет встроенных TVF) | — |
| MariaDB | — (нет встроенных TVF) | — |
| SQLite | — (нет встроенных TVF) | — |
| ClickHouse | `numbers`, `numbers_mt`, `zeros`, `zeros_mt`, `generateRandom`, `values`, … | `zeros(N)`/`zeros_mt(N)` — таблица с единственной колонкой `zero UInt8` |
| InMemory | — (бросает `NotSupportedException`) | — |

Единообразие провайдеров: `zeros`/`zeros_mt` — ClickHouse-only (нет аналога ни в одном другом
диалекте), поэтому метод идёт на `ClickHouseFunctions`, а гейт — `SupportsTableFunction("zeros"/
"zeros_mt")`. Ровно как `numbers`.

Рассмотренные и отклонённые (обоснование из документации ClickHouse):

| Функция | Почему не реализована |
|---|---|
| `generateRandom([structure, seed, …])` | Схема задаётся строкой → динамическая форма строки; статический `IQueryable<T>` не выражает её. Пользователь может объявить свой `[SqlTableFunction("generateRandom")]`-обёртку (пользовательские обёртки не гейтятся). |
| `values(...)` | Схема/значения — строки формата; динамическая форма. |
| `url`, `s3`, `remote`/`remoteSecure`, `file`, `format`, `merge`, `input` | Требуют конфигурации сервера/внешнего доступа или динамической схемы — вне объёма (как в backlog). |
| `cluster`/`clusterAllReplicas` | Требуют сконфигурированного кластера. |
| `system.numbers` | Уже покрыто табличной функцией `numbers`; `system.numbers` — системная таблица, а не табличная функция. |
| `system.one` | Системная таблица (аналог `DUAL`), без аргументов; механизм `FromTableFunction` всегда рендерит `name(...)`, т.е. `system.one()`. Проверено на реальном ClickHouse: `system.one()` не принимается (валиден только `FROM system.one`). Вне объёма. |
| `system.zeros`/`system.zeros_mt` | Дублируют `zeros`/`zeros_mt`, доступны как системные таблицы. |

## Ближайший аналог C# и tier

- Прямого BCL-аналога нет (это источник строк, а не скаляр). Это tier (b): новый метод на
  `ClickHouseFunctions` с `[SqlTableFunction]` + row-интерфейс + гейт `SupportsTableFunction`.
- Ближайший образец — `numbers`/`numbers_mt` (`SqlFunctions.ClickHouse.cs:152-168`): тот же
  `[SqlTableFunction]` + `IQueryable<row>` + `INumbersRow` в `SqlFunctions.cs`.

## Семантика

- `zeros(N)` — таблица из `N` строк с единственной колонкой `zero` типа `UInt8` (значение `0`).
- `zeros_mt(N)` — то же, но многопоточно.
- CLR-тип колонки — `byte` (`UInt8`); row reader читает его через `GetByte`
  (`SelectExpression.cs`), конвертация не нужна (в отличие от `numbers` с `UInt64`).

## План по коду

- `SqlFunctions.cs`: `IZerosRow { [Column("zero")] byte Value }`.
- `SqlFunctions.ClickHouse.cs` (`ClickHouseFunctions`):
  `[SqlTableFunction("zeros")] IQueryable<SqlFunctions.IZerosRow> zeros(long count)`,
  `[SqlTableFunction("zeros_mt")] IQueryable<SqlFunctions.IZerosRow> zeros_mt(long count)`.
- `ClickHouseDialect.SupportsTableFunction`: `name is "numbers" or "numbers_mt" or "zeros" or
  "zeros_mt"`.
- Обёртка `WrapTableFunction` не нужна: `UInt8` материализуется как `byte`.

## Публичный API

Два новых публичных метода на `ClickHouseFunctions` и один публичный row-интерфейс
`SqlFunctions.IZerosRow` (+ `byte Value`). XML-doc обязателен.

## План тестов

- ClickHouse SQL-gen: `FromTableFunction(() => zeros(3))` → `from zeros(@count)`.
- ClickHouse dialect: `SupportsTableFunction("zeros")`/`("zeros_mt")` → `true`.
- PostgreSQL: `BuiltInTableFunction_Zeros_UnsupportedByProvider_ShouldThrow` (гейт).
- ClickHouse integration (реальный контейнер): `zeros(3)` возвращает 3 строки со значением `0`.

## Покрытие

- Baseline: 84.8% line / 72.8% branch (см. `docs/providers/clickhouse.md`). ClickHouse-код в
  `coverage.settings.xml` не входит, поэтому число не сдвинется; добавляются SQL-gen/dialect тесты.

## Документация и спеки

- `docs/guide/13-table-valued-functions.md` + RU — строка про `zeros`/`zeros_mt`.
- `docs/providers/clickhouse.md` + RU — список табличных функций.
- `docs/advanced/api-reference.md` + RU — строка ClickHouse.
- `docs/specs/roadmap/todo_clickhouse.md` — `[x]` для `zeros`/`zeros_mt` + остаток.
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` — строка TVF.

## Файлы

1. `src/nextorm.core/Query/SqlFunctions.cs`, `SqlFunctions.ClickHouse.cs`
2. `src/nextorm.clickhouse/ClickHouseDialect.cs`
3. Тесты: `tests/nextorm.clickhouse.tests/*`, `tests/nextorm.postgres.tests/*`,
   `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`
4. docs EN+RU, `todo_clickhouse.md`, gap-analysis

## Результат (19.09.2026)

Реализованы `zeros(N)`/`zeros_mt(N)`: row-тип `SqlFunctions.IZerosRow` (`[Column("zero")] byte Value`),
методы `ClickHouseFunctions.zeros`/`zeros_mt`, `SupportsTableFunction("zeros"/"zeros_mt")`. Обёртка
`WrapTableFunction` не потребовалась: `UInt8` материализуется напрямую как `byte`. Прочие табличные
функции отклонены обоснованно (динамическая схема / конфигурация сервера); `system.one()` проверен на
реальном ClickHouse 25.8 — `Code: 46 Unknown table function system.one`, поэтому как табличная функция
не добавлен.

`dotnet build nextorm.sln -c Release` — 0/0; ClickHouse unit 96/96; PostgreSQL unit 171/171;
`ClickHouseIntegrationTests.ZerosTableFunction_ShouldReturnThreeRows` (реальный ClickHouse) — прошёл.
WIP закрыт.
