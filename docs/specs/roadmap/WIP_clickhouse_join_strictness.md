# WIP: ClickHouse join strictness (`ANY`/`ALL`/`ASOF`/`SEMI`/`ANTI`/`PASTE`)

> Рабочий план по скилу `implementing-todo-features`. Источник: `todo_clickhouse.md` →
> «Уровень 3 → Join'ы ClickHouse».
> **Статус: `ANY`/`ALL`/`ASOF` реализованы; `SEMI`/`ANTI`/`PASTE` — остались** (см. «Результат»).

## Пункт и цель

- Фича: модификаторы join ClickHouse — strictness `ANY`/`ALL`, `ASOF`, а также `SEMI`/`ANTI` и
  `PASTE JOIN`.
- Провайдер: ClickHouse-only (у остальных диалектов аналогов нет).
- Критерий приёмки: fluent-модификатор помечает join, `MakeJoinKeyword` рендерит
  `<type> <strictness> join`, гейт `SupportsJoinStrictness` отклоняет остальных; in-memory бросает;
  SQL-gen + реальный ClickHouse зелёные.

## Провайдер × форма (шаг 1)

Источник — официальная документация ClickHouse
([`JOIN`](https://clickhouse.com/docs/en/sql-reference/statements/select/join)) и справочники
PostgreSQL/SQL Server/MySQL/MariaDB/SQLite.

Синтаксис ClickHouse: `[GLOBAL] [INNER|LEFT|RIGHT|FULL|CROSS] [OUTER|SEMI|ANTI|ANY|ALL|ASOF] JOIN ...`.

| Провайдер | `ANY`/`ALL` | `SEMI`/`ANTI` | `ASOF` | `PASTE` |
|---|---|---|---|---|
| PostgreSQL | — | — | — (нет `ASOF JOIN`) | — |
| SQL Server | — | — | — | — |
| MySQL | — | — | — | — |
| MariaDB | — | — | — | — |
| SQLite | — | — | — | — |
| ClickHouse | `LEFT/RIGHT/INNER/FULL ANY`/`ALL` | `LEFT/RIGHT SEMI`/`ANTI` | `ASOF JOIN`, `LEFT ASOF JOIN` | `PASTE JOIN` |
| InMemory | — (LINQ join) | — | — | — |

Единообразие: всё ClickHouse-only → отдельные флаги/хуки, остальные бросают.

## Выбранный API (вариант 1) и реализация

Публичный join-API (`EntityBuilder<TEntity>`, `EntityBuilder<TableAlias>`,
`JoinedEntityBuilder<T1..T8>`) не принимает `JoinType` — `JoinCore` приватный. Выбран **мутирующий
модификатор** `WithStrictness(JoinStrictness)` (вместо ~40 `Any*`/`All*`-методов):

- `JoinStrictness { Default, Any, All, Asof }` и `JoinExpression.Strictness` (`internal set`).
- `EntityBuilder<TEntity>.WithStrictness(...)` копирует билдер (`Clone()`) и заменяет его последний
  join на **копию** `JoinExpression` с модификатором; исходный билдер и его другие клоны не
  меняются. Бросает `InvalidOperationException`, если join'а ещё нет. Он отдаёт клон, поэтому на
  каждом `JoinedEntityBuilder<T1..T8>` добавлена ковариантная `new`-перегрузка, возвращающая
  конкретный arity (сохраняет плоскую цепочку `.LeftJoin(...).WithStrictness(...).LeftJoin(...)`).
  Для arity 2 есть виртуальный `OnLastJoinReplaced`, который переводит `JoinCondition` на новый
  объект — иначе `JoinCore` добавил бы в следующий join исходный `JoinCondition` без модификатора.
  (Первая версия мутировала join на месте; по аудиту — Находка 16 P1 — заменена на копирование,
  чтобы не текло в соседние билдеры и план-ключ.)
- `ISqlDialect.SupportsJoinStrictness` (default `false`; ClickHouse `true`) +
  `string MakeJoinKeyword(JoinType, JoinStrictness)` (base — текущий switch и `NotSupportedException`
  на non-default; ClickHouse — `<type> [any|all|asof] join`).
- `SqlSourceRenderer.MakeJoin` использует `MakeJoinKeyword`; отклоняет модификатор у провайдера без
  флага и на `CROSS`/`CROSS`/`APPLY`-join'ах (модификатор допустим только на
  `INNER`/`LEFT`/`RIGHT`/`FULL`).
- `JoinExpressionPlanEqualityComparer` учитывает `Strictness` в `Equals`/`GetHashCode`; `CloneForCache`
  переносит его. `InMemoryJoin` бросает `NotSupportedException`.

ClickHouse-синтаксис: `[INNER|LEFT|RIGHT|FULL] [ANY|ALL|ASOF] JOIN` (модификатор после типа).
Семантика ClickHouse: `LEFT ANY JOIN` — по одной правой строке на левую; `INNER ANY JOIN` —
одна строка на уникальный ключ; `ASOF` требует как минимум одной equi-колонки и одной
неравенства последней.

## Остаток: `SEMI`/`ANTI`/`PASTE`

- `SEMI`/`ANTI` меняют набор колонок (только левая таблица), что несовместимо с
  `Projection<T1,T2>` — нужен отдельный результат (проекция только левых колонок) либо запрет
  проекции правых. Не реализовано.
- `PASTE JOIN` — cross-подобный join без `ON`; отдельная конструкция. Не реализовано.

## Тесты и результат

- `ClickHouseDialectTests.JoinStrictness_ShouldRenderClickHouseModifiers` (флаг + `MakeJoinKeyword`).
- `SqlGenerationTests.JoinWithStrictness_ShouldRenderClickHouseModifier`,
  `JoinStrictness_OnCrossJoin_ShouldThrow`, `WithStrictness_WithoutJoin_ShouldThrow`,
  `WithStrictness_ShouldNotMutateSourceBuilder`, `WithStrictness_ThenJoin_ShouldKeepStrictnessOnFirstJoin`
  (регресс на Находку 16).
- `Postgres…JoinStrictness_UnsupportedByProvider_ShouldThrow`,
  `InMemoryJoinTests.TestJoinStrictness_ShouldThrow`.
- `ClickHouseIntegrationTests.AnyStrictness_ShouldKeepSingleMatch`,
  `AllStrictness_ShouldKeepEveryMatch`, `AsofJoin_ShouldPickClosestMatch` (реальный ClickHouse 25.8).
- Release 0/0; все unit-наборы зелёные (ClickHouse 107, PostgreSQL 175, core 158, …); все три
  интеграционных теста прошли. Coverage: 84.7% line / 73% branch (порог 75%).
- Аудит `nextorm-code-auditor`: Находка 16 (P1, in-place мутация) исправлена (copy-on-write +
  `OnLastJoinReplaced`), Находка 17 (P2, `CloneForCache From = From`) исправлена попутно.

## Документация и спеки

- `docs/providers/clickhouse.md` + RU, `docs/advanced/api-reference.md` + RU,
  `sql-capabilities-gap-analysis.md` (Join-строки), `todo_clickhouse.md`.

## Файлы

1. `src/nextorm.core/Expressions/JoinExpression.cs` (`JoinStrictness`, `Strictness`)
2. `src/nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs`
3. `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`
4. `src/nextorm.clickhouse/ClickHouseDialect.cs`
5. `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `InMemoryJoin.cs`
6. `src/nextorm.core/Builders/EntityBuilder.cs`, `Builders/Joins/JoinedEntityBuilder.cs`
7. Тесты + docs EN+RU
