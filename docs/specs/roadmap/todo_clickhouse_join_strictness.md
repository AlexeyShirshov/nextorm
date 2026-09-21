# TODO: ClickHouse join strictness — остаток (`SEMI`/`ANTI`/`PASTE`)

> Actionable-остаток ClickHouse join-воркстрима. `ANY`/`ALL`/`ASOF` и мутирующий модификатор
> `WithStrictness(JoinStrictness)` реализованы.
> **Статус: заблокировано** API-дизайном: не выбран результат для join'ов, меняющих набор колонок.

## Что уже сделано (не переделывать)

- `JoinStrictness { Default, Any, All, Asof }`, `JoinExpression.Strictness`,
  `EntityBuilder.WithStrictness(...)` (copy-on-write: клонирует билдер и заменяет последний join на
  копию; `OnLastJoinReplaced` для arity 2), ковариантные `new`-перегрузки на `JoinedEntityBuilder<T1..T8>`.
- `ISqlDialect.SupportsJoinStrictness` (default `false`; ClickHouse `true`) +
  `MakeJoinKeyword(JoinType, JoinStrictness)` (`<type> [any|all|asof] join`); `SqlSourceRenderer.MakeJoin`
  использует хук и отклоняет модификатор на `CROSS`/`APPLY`.
- `JoinExpressionPlanEqualityComparer` учитывает `Strictness`; `CloneForCache` переносит его;
  in-memory бросает `NotSupportedException`.

## Блокер

- **`SEMI` / `ANTI` меняют набор колонок** (результат — только левая таблица), что несовместимо с
  `Projection<T1, T2>`: нужен отдельный результат (проекция только левых колонок) либо запрет
  проекции правых.
- **`PASTE JOIN`** — cross-подобный join без `ON`; отдельная конструкция, не выражается текущим
  `JoinType`/`JoinExpression`.

## Что нужно решить (API-дизайн)

1. **Результат `SEMI`/`ANTI`.** Отдельный терминал/билдер, отдающий только левые колонки (например,
   `SemiJoin`/`AntiJoin` → `EntityBuilder<T1>` без правого параметра) — либо правило «проецировать
   можно только левые колонки» с явной ошибкой на правые.
2. **`PASTE JOIN`.** Отдельная конструкция `PasteJoin` без `ON`, результат `Projection<T1, T2>` по
   позиции (порядок колонок = конкатенация левой и правой).
3. **Хуки.** Расширить `JoinStrictness` (`Semi`/`Anti`) и/или ввести отдельный enum/вид для `Paste`;
   `MakeJoinKeyword` рендерит `semi join`/`anti join`/`paste join`; отдельные `Supports*`-гейты
   (CH-only), остальные бросают.
4. **Инфраструктура.** План-ключ (`JoinExpressionPlanEqualityComparer`), `CloneForCache`, in-memory
   `NotSupportedException`, валидация комбинаций с `JoinType`.

## Матрица «провайдер × форма»

Синтаксис ClickHouse: `[GLOBAL] [INNER|LEFT|RIGHT|FULL|CROSS] [OUTER|SEMI|ANTI|ANY|ALL|ASOF] JOIN ...`.

| Провайдер | `ANY`/`ALL` | `SEMI`/`ANTI` | `ASOF` | `PASTE` |
|---|---|---|---|---|
| PostgreSQL | — | — | — | — |
| SQL Server | — | — | — | — |
| MySQL | — | — | — | — |
| MariaDB | — | — | — | — |
| SQLite | — | — | — | — |
| ClickHouse | `LEFT/RIGHT/INNER/FULL ANY`/`ALL` | `LEFT/RIGHT SEMI`/`ANTI` | `ASOF JOIN`, `LEFT ASOF JOIN` | `PASTE JOIN` |
| InMemory | — | — | — | — |

## Критерий приёмки

- `SEMI`/`ANTI`/`PASTE` помечаются fluent-модификатором, `MakeJoinKeyword` рендерит корректный
  T-SQL/ClickHouse-синтаксис, результат имеет согласованный набор колонок; прочие провайдеры
  отклоняют, in-memory бросает.
- SQL-gen + реальный ClickHouse 25.8 зелёные; покрытие ≥ `MIN_LINE_COVERAGE`; аудит
  `nextorm-code-auditor`; docs EN+RU + `sql-capabilities-gap-analysis.md`.

## Источники и файлы

- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.8.
- https://clickhouse.com/docs/en/sql-reference/statements/select/join
- Код: `src/nextorm.core/Expressions/JoinExpression.cs`,
  `JoinExpressionPlanEqualityComparer.cs`, `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`,
  `SqlDialectBase.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`,
  `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `InMemoryJoin.cs`,
  `src/nextorm.core/Builders/EntityBuilder.cs`, `Builders/Joins/JoinedEntityBuilder.cs`.
